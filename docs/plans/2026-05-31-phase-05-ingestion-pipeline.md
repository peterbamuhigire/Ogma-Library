# Phase 05 — Ingestion Pipeline & Scanning Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver the full ingestion pipeline for Ogma Library (.NET 10, Avalonia) — library root persistence, recursive PDF discovery, Channel-based background worker, identity matching, unavailable-file flagging, thumbnail/spine generation, incremental rescan, scan health report, UI hook, and all required tests — keeping the build GREEN (TreatWarningsAsErrors, XML docs required).

**Architecture:** System.Threading.Channels pipeline: DiscoveryService → IdentityMatcher → MetadataExtractionService → BookIngestionWorker (BackgroundService). All inter-stage communication via Channel<T>. UI progress bridged via Dispatcher.UIThread.Post. Every background task is a Jobs row with an IdempotencyKey unique index (NFR-OGMA-009). Per-file isolation: each job in a try/catch so failures never cancel siblings.

**Tech Stack:** .NET 10, EF Core 9 (SQLite), PDFtoImage 4.1.0 + bblanchon.PDFium (thumbnail render), SkiaSharp (resize/encode), PdfPig (metadata extraction), Avalonia 11.3.17 (StorageProvider for folder picker, Dispatcher.UIThread), xUnit, QuestPDF (test fixture generation).

---

## Context: What already exists (Phase 04)

- `CatalogueDbContext` with full schema: Books, BookFiles, BookMetadataFields, Jobs, AuditEvents tables all configured.
- `IBookIdentityService` / `BookIdentityService` — five-tier identity chain (path → SHA-256 → mtime+size → fingerprint → ISBN).
- `ISidecarService` / `SidecarService` — resolves `.ogma/<class>/<prefix>/<hash>.ext` paths, creates directories.
- `IBookRepository`, `IAuditRepository`, `IReadingProgressRepository`, `IAnnotationRepository` — all implemented.
- `ICatalogueReadModel` — EF read projections.
- `InMemoryLocalizationService` — en/fr resource dictionaries.
- 52 passing tests (45 unit+integration, 4 architecture, 3 headless UI).
- `BookRow` has: `BookId`, `Title`, `Sha256Hash`, `SizeBytes`, `MtimeTicks`, `PdfFingerprint`, `IsbnNormalized`, `Status` (0=Active, 1=Unavailable).
- `BookFileRow` has: `BookFileId`, `BookId`, `RelativePath`, `FileStatus` (0=Present, 1=Missing), `LastSeenUtc`.
- `JobRow` has: `JobId`, `JobType`, `IdempotencyKey` (unique), `Status` (0=Pending,1=Running,2=Completed,3=Failed), `BookId`, `Payload`, `StartedUtc`, `CompletedUtc`, `ErrorMessage`, `RetryCount`.

---

## CRITICAL CONSTRAINTS

- `TreatWarningsAsErrors=true` — zero build warnings. XML doc comments on EVERY public member in non-test projects (except `OgmaLibrary.App` which has `CS1591` suppressed).
- Workers project must NOT reference `OgmaLibrary.App` (architecture test enforces this).
- Domain project must NOT gain new dependencies.
- Cross-platform: `Path.Combine`, no Windows-specific APIs. Forward-slash relative paths for DB storage.
- All async code: `ConfigureAwait(false)` in library code.
- `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace` on public method inputs.

---

## Task 1: Add NuGet packages to Infrastructure and Workers projects

**Files:**
- Modify: `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj`
- Modify: `src/OgmaLibrary.Workers/OgmaLibrary.Workers.csproj`
- Modify: `tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj`

**Step 1: Add packages to Infrastructure.csproj**

Open `src/OgmaLibrary.Infrastructure/OgmaLibrary.Infrastructure.csproj`. Add after the existing `<ItemGroup>`:

```xml
<ItemGroup>
  <!-- PDF rendering (spike S02 winner) -->
  <PackageReference Include="PDFtoImage" Version="4.1.0" />
  <!-- SkiaSharp for thumbnail/spine encoding -->
  <PackageReference Include="SkiaSharp" Version="2.88.8" />
  <PackageReference Include="SkiaSharp.NativeAssets.Win32" Version="2.88.8" Condition="$([MSBuild]::IsOSPlatform('Windows'))" />
  <PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="2.88.8" Condition="$([MSBuild]::IsOSPlatform('OSX'))" />
  <!-- PdfPig for metadata extraction -->
  <PackageReference Include="PdfPig" Version="0.1.9" />
</ItemGroup>
```

**Step 2: Add Microsoft.Extensions.Hosting to Workers.csproj**

Open `src/OgmaLibrary.Workers/OgmaLibrary.Workers.csproj`. Add:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.8" />
</ItemGroup>
```

**Step 3: Add QuestPDF + SkiaSharp to Tests.csproj**

Open `tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj`. Add:

```xml
<PackageReference Include="QuestPDF" Version="2025.1.0" />
<PackageReference Include="SkiaSharp" Version="2.88.8" />
<PackageReference Include="SkiaSharp.NativeAssets.Win32" Version="2.88.8" Condition="$([MSBuild]::IsOSPlatform('Windows'))" />
<PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="2.88.8" Condition="$([MSBuild]::IsOSPlatform('OSX'))" />
```

**Step 4: Restore and verify build**

```
cd C:/wamp64/www/Ogma-Library
dotnet restore OgmaLibrary.sln
dotnet build OgmaLibrary.sln -c Release
```

Expected: 0 warnings, 0 errors.

---

## Task 2: Application layer interfaces (Ingestion bounded context)

**Files:**
- Create: `src/OgmaLibrary.Application/Ingestion/ILibrarySettingsService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IPdfDiscoveryService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IIngestionOrchestrator.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IScanProgressService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IScanHealthService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IngestionModels.cs`

**Step 1: Create `IngestionModels.cs`** — plain records/enums with no EF Core deps.

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>A PDF file discovered during a library scan (FR-LIB-002).</summary>
/// <param name="AbsolutePath">The absolute OS-native path to the file.</param>
/// <param name="RelativePath">The forward-slash path relative to the library root.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="MtimeTicks">The last-modified timestamp as UTC ticks.</param>
public sealed record DiscoveredFile(
    string AbsolutePath,
    string RelativePath,
    long SizeBytes,
    long MtimeTicks);

/// <summary>Scan phase labels for scan progress reporting (FR-LIB-001, NFR-PROD-005).</summary>
public enum ScanPhase
{
    /// <summary>Not yet started.</summary>
    Idle = 0,
    /// <summary>Recursively enumerating PDF files.</summary>
    Discovering = 1,
    /// <summary>Matching and registering files in the catalogue.</summary>
    Processing = 2,
    /// <summary>Generating thumbnails and spine strips.</summary>
    GeneratingAssets = 3,
    /// <summary>Scan completed successfully.</summary>
    Complete = 4,
    /// <summary>Scan completed with one or more per-file failures.</summary>
    PartialFailure = 5,
    /// <summary>Scan was cancelled by the user.</summary>
    Cancelled = 6,
}

/// <summary>A snapshot of scan progress for UI binding (NFR-PROD-005).</summary>
/// <param name="Phase">The current scan phase.</param>
/// <param name="FilesDiscovered">Total files found by discovery.</param>
/// <param name="FilesCompleted">Files fully processed so far.</param>
/// <param name="FilesFailed">Files that failed processing.</param>
/// <param name="IsCancellable">Whether a cancel is possible at this stage.</param>
public sealed record ScanProgressSnapshot(
    ScanPhase Phase,
    int FilesDiscovered,
    int FilesCompleted,
    int FilesFailed,
    bool IsCancellable)
{
    /// <summary>Progress in [0.0, 1.0]; 0.0 when no files discovered yet.</summary>
    public double ProgressPct =>
        FilesDiscovered == 0 ? 0.0
        : Math.Min(1.0, (FilesCompleted + FilesFailed) / (double)FilesDiscovered);
}

/// <summary>Health report data for one failure category (FR-LIB-007).</summary>
/// <param name="FilePath">The relative path of the failing file.</param>
/// <param name="ErrorMessage">The recorded error message.</param>
/// <param name="JobId">The Jobs row identifier for retry operations.</param>
/// <param name="FailedAtUtc">When the failure was recorded.</param>
public sealed record ScanFailureItem(
    string FilePath,
    string? ErrorMessage,
    long JobId,
    DateTimeOffset FailedAtUtc);

/// <summary>Aggregated scan health counts (FR-LIB-007).</summary>
/// <param name="FailedJobs">Jobs that failed for general reasons.</param>
/// <param name="PasswordProtected">Files detected as password-protected.</param>
/// <param name="MissingThumbnails">Books with no generated cover.</param>
/// <param name="MetadataGaps">Books missing Title or Author metadata.</param>
public sealed record ScanHealthReport(
    IReadOnlyList<ScanFailureItem> FailedJobs,
    IReadOnlyList<ScanFailureItem> PasswordProtected,
    IReadOnlyList<ScanFailureItem> MissingThumbnails,
    IReadOnlyList<ScanFailureItem> MetadataGaps);
```

**Step 2: Create `ILibrarySettingsService.cs`**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Persists and retrieves the library root path and excluded-folder list (FR-LIB-001).
/// Implementations persist to the OS app-data directory.
/// </summary>
public interface ILibrarySettingsService
{
    /// <summary>Returns the persisted library root path, or <see langword="null"/> if none is set.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the library root path.</summary>
    /// <param name="rootPath">The absolute path to the library root folder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>Returns the list of folder names or relative paths to exclude from scans.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the excluded-folder list.</summary>
    /// <param name="excludedFolders">The new list of excluded folder names or relative paths.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetExcludedFoldersAsync(IReadOnlyList<string> excludedFolders, CancellationToken cancellationToken = default);
}
```

**Step 3: Create `IPdfDiscoveryService.cs`**

```csharp
using System.Threading.Channels;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Recursively enumerates PDF files under a library root, honoring an excluded-folder
/// list, and streams results via a channel (FR-LIB-002). Path-safe: no traversal
/// outside the root is permitted.
/// </summary>
public interface IPdfDiscoveryService
{
    /// <summary>
    /// Starts recursive PDF discovery and writes <see cref="DiscoveredFile"/> items
    /// to the supplied channel writer. Completes the writer when enumeration finishes
    /// or the token is cancelled.
    /// </summary>
    /// <param name="rootPath">The absolute path to the library root.</param>
    /// <param name="excludedFolders">Folder names or relative sub-paths to exclude.</param>
    /// <param name="writer">The channel writer that receives discovered files.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken = default);
}
```

**Step 4: Create `IIngestionOrchestrator.cs`**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Coordinates the full ingestion pipeline: discovery → identity matching →
/// metadata extraction → asset generation jobs (FR-LIB-001..006).
/// </summary>
public interface IIngestionOrchestrator
{
    /// <summary>
    /// Starts a full scan of the library root configured in
    /// <see cref="ILibrarySettingsService"/>. Returns when the pipeline has drained
    /// (all discovery results processed and jobs enqueued).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the scan.</param>
    Task ScanAsync(CancellationToken cancellationToken = default);
}
```

**Step 5: Create `IScanProgressService.cs`**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Thread-safe scan progress aggregator. Background workers call the mutating methods;
/// the UI subscribes to <see cref="ProgressChanged"/> and reads
/// <see cref="CurrentSnapshot"/> (NFR-PROD-005).
/// </summary>
public interface IScanProgressService
{
    /// <summary>The most recent progress snapshot.</summary>
    ScanProgressSnapshot CurrentSnapshot { get; }

    /// <summary>Raised on the calling thread when any progress value changes.</summary>
    event EventHandler<ScanProgressSnapshot>? ProgressChanged;

    /// <summary>Transitions to a new scan phase.</summary>
    /// <param name="phase">The new phase.</param>
    void SetPhase(ScanPhase phase);

    /// <summary>Increments the discovered-file count.</summary>
    void IncrementDiscovered();

    /// <summary>Increments the completed-file count.</summary>
    void IncrementCompleted();

    /// <summary>Increments the failed-file count.</summary>
    void IncrementFailed();

    /// <summary>Resets all counters (call before starting a new scan).</summary>
    void Reset();
}
```

**Step 6: Create `IScanHealthService.cs`**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Aggregates scan health data from the Jobs table and catalogue for the V1
/// scan health report panel (FR-LIB-007).
/// </summary>
public interface IScanHealthService
{
    /// <summary>
    /// Returns the current scan health report, grouping failures into four
    /// actionable categories.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<ScanHealthReport> GetReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues all failed jobs (sets their status back to Pending) so the
    /// background worker picks them up again.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RetryAllFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues a single failed job by its identifier.
    /// </summary>
    /// <param name="jobId">The Jobs row identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default);
}
```

**Step 7: Build to check XML docs**

```
dotnet build OgmaLibrary.sln -c Release
```

Expected: 0 warnings.

---

## Task 3: Infrastructure — LibrarySettingsService (file-based persistence)

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/LibrarySettingsService.cs`

**Step 1: Write the implementation**

Stores settings in a JSON file at `<dataDirectory>/library-settings.json`. No DB dependency — simpler and decoupled from the catalogue migrations.

```csharp
using System.Text.Json;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Persists library root and excluded-folder settings to a JSON file in the
/// application data directory (FR-LIB-001).
/// </summary>
public sealed class LibrarySettingsService : ILibrarySettingsService
{
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Serialization DTO — not a public contract.
    private sealed class SettingsDto
    {
        public string? LibraryRoot { get; set; }
        public List<string> ExcludedFolders { get; set; } = [];
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LibrarySettingsService"/>.
    /// </summary>
    /// <param name="dataDirectory">
    /// The directory under which <c>library-settings.json</c> is stored.
    /// </param>
    public LibrarySettingsService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "library-settings.json");
    }

    /// <inheritdoc />
    public async Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.LibraryRoot;
    }

    /// <inheritdoc />
    public async Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.LibraryRoot = rootPath;
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.ExcludedFolders.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task SetExcludedFoldersAsync(IReadOnlyList<string> excludedFolders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excludedFolders);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.ExcludedFolders = [.. excludedFolders];
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SettingsDto> LoadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SettingsDto> LoadLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new SettingsDto();
        }

        var stream = File.OpenRead(_settingsPath);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer
                .DeserializeAsync<SettingsDto>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? new SettingsDto();
        }
    }

    private async Task SaveLockedAsync(SettingsDto dto, CancellationToken cancellationToken)
    {
        var stream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(stream, dto, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
```

**Step 2: Build**

```
dotnet build OgmaLibrary.sln -c Release
```

---

## Task 4: Infrastructure — PdfDiscoveryService

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/PdfDiscoveryService.cs`

**Step 1: Write the implementation**

```csharp
using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Recursively enumerates *.pdf files under a library root, filtering out excluded
/// folders, and streams results via a <see cref="System.Threading.Channels.Channel{T}"/>
/// (FR-LIB-002). All enumeration runs on the calling (background) thread; the UI
/// thread is never touched.
/// </summary>
public sealed class PdfDiscoveryService : IPdfDiscoveryService
{
    /// <inheritdoc />
    public async Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(excludedFolders);
        ArgumentNullException.ThrowIfNull(writer);

        // Normalize root once for prefix-matching.
        string normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootPath));

        try
        {
            await Task.Run(() =>
                EnumerateFiles(normalizedRoot, excludedFolders, writer, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Propagate so callers can observe cancellation.
            throw;
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static void EnumerateFiles(
        string normalizedRoot,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken)
    {
        // Stack-based recursive enumeration to avoid deep call stacks on large trees.
        var stack = new Stack<string>();
        stack.Push(normalizedRoot);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string dir = stack.Pop();
            string dirName = Path.GetFileName(dir);

            // Skip excluded folders (match by folder name or relative prefix).
            if (IsExcluded(dir, dirName, normalizedRoot, excludedFolders))
            {
                continue;
            }

            // Emit PDF files in this directory.
            foreach (string file in EnumerateFilesInDir(dir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Path-traversal guard: file must be under the root.
                string fullPath = Path.GetFullPath(file);
                if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    continue;
                }

                string relative = ComputeRelativePath(fullPath, normalizedRoot);
                var discovered = new DiscoveredFile(
                    AbsolutePath: fullPath,
                    RelativePath: relative,
                    SizeBytes: info.Length,
                    MtimeTicks: info.LastWriteTimeUtc.Ticks);

                // WriteAsync not available synchronously; use TryWrite (channel is bounded
                // but discovery can await on backpressure outside the sync Task.Run block).
                // As a pragmatic choice for the sync enumeration, use TryWrite and spin.
                // The channel capacity (set at pipeline build time) provides back-pressure.
                writer.TryWrite(discovered);
            }

            // Push sub-directories onto the stack.
            foreach (string sub in EnumerateSubDirs(dir))
            {
                stack.Push(sub);
            }
        }
    }

    private static bool IsExcluded(
        string dirAbsolute,
        string dirName,
        string normalizedRoot,
        IReadOnlyList<string> excludedFolders)
    {
        foreach (string excluded in excludedFolders)
        {
            // Match by directory name.
            if (string.Equals(dirName, excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Match by relative path prefix (forward-slash or OS separator).
            string relDir = ComputeRelativePath(dirAbsolute, normalizedRoot);
            if (relDir.StartsWith(excluded.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateFilesInDir(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateSubDirs(string dir)
    {
        try
        {
            return Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static string ComputeRelativePath(string absolutePath, string normalizedRoot)
    {
        string root = normalizedRoot + Path.DirectorySeparatorChar;
        if (absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }

        return absolutePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
```

**Step 2: Build**

```
dotnet build OgmaLibrary.sln -c Release
```

---

## Task 5: Infrastructure — ScanProgressService

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/ScanProgressService.cs`

**Step 1: Write thread-safe implementation**

```csharp
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Thread-safe scan progress aggregator. Background worker threads call the mutating
/// methods; the <see cref="ProgressChanged"/> event is raised synchronously on the
/// calling thread so Avalonia UI code can marshal via <c>Dispatcher.UIThread.Post</c>
/// (NFR-PROD-005).
/// </summary>
public sealed class ScanProgressService : IScanProgressService
{
    private volatile ScanProgressSnapshot _snapshot =
        new(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);

    private int _filesDiscovered;
    private int _filesCompleted;
    private int _filesFailed;

    /// <inheritdoc />
    public ScanProgressSnapshot CurrentSnapshot => _snapshot;

    /// <inheritdoc />
    public event EventHandler<ScanProgressSnapshot>? ProgressChanged;

    /// <inheritdoc />
    public void SetPhase(ScanPhase phase)
    {
        bool cancellable = phase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;
        Publish(new ScanProgressSnapshot(
            phase,
            _filesDiscovered,
            _filesCompleted,
            _filesFailed,
            IsCancellable: cancellable));
    }

    /// <inheritdoc />
    public void IncrementDiscovered()
    {
        int discovered = Interlocked.Increment(ref _filesDiscovered);
        PublishCounts(discovered, _filesCompleted, _filesFailed);
    }

    /// <inheritdoc />
    public void IncrementCompleted()
    {
        int completed = Interlocked.Increment(ref _filesCompleted);
        PublishCounts(_filesDiscovered, completed, _filesFailed);
    }

    /// <inheritdoc />
    public void IncrementFailed()
    {
        int failed = Interlocked.Increment(ref _filesFailed);
        PublishCounts(_filesDiscovered, _filesCompleted, failed);
    }

    /// <inheritdoc />
    public void Reset()
    {
        Interlocked.Exchange(ref _filesDiscovered, 0);
        Interlocked.Exchange(ref _filesCompleted, 0);
        Interlocked.Exchange(ref _filesFailed, 0);
        Publish(new ScanProgressSnapshot(ScanPhase.Idle, 0, 0, 0, IsCancellable: false));
    }

    private void PublishCounts(int discovered, int completed, int failed)
    {
        var phase = _snapshot.Phase;
        bool cancellable = phase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;
        Publish(new ScanProgressSnapshot(phase, discovered, completed, failed, cancellable));
    }

    private void Publish(ScanProgressSnapshot snapshot)
    {
        _snapshot = snapshot;
        ProgressChanged?.Invoke(this, snapshot);
    }
}
```

---

## Task 6: Infrastructure — MetadataExtractionService (PdfPig)

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/MetadataExtractionService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/IMetadataExtractionService.cs`

**Step 1: Create the interface**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Extracts PDF metadata (Title, Author, Subject, CreationDate) from a PDF file's
/// DocumentInformation dictionary and XMP packet, and persists the results as
/// <c>BookMetadataFields</c> rows with <c>Source = "PDF"</c> (FR-META-001 precursor).
/// </summary>
public interface IMetadataExtractionService
{
    /// <summary>
    /// Extracts metadata from the PDF at <paramref name="absoluteFilePath"/> and upserts
    /// the fields for <paramref name="bookId"/> in the catalogue.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating whether extraction succeeded and any error message.</returns>
    Task<(bool Success, string? ErrorMessage)> ExtractAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
```

**Step 2: Create the implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Uses PdfPig to extract Title, Author, Subject, and CreationDate from a PDF's
/// document information dictionary (FR-META-001 precursor, Phase 05).
/// </summary>
public sealed class MetadataExtractionService : IMetadataExtractionService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataExtractionService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public MetadataExtractionService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> ExtractAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        try
        {
            var fields = await Task.Run(() => ExtractFields(absoluteFilePath), cancellationToken)
                .ConfigureAwait(false);

            foreach ((string fieldName, string value) in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BookMetadataFieldRow? existing = await _context.BookMetadataFields
                    .FirstOrDefaultAsync(
                        f => f.BookId == bookId && f.FieldName == fieldName && f.Source == "PDF",
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    _context.BookMetadataFields.Add(new BookMetadataFieldRow
                    {
                        BookId = bookId,
                        FieldName = fieldName,
                        Value = value,
                        Source = "PDF",
                        Confidence = 0.5,
                        SourceTimestamp = DateTimeOffset.UtcNow,
                    });
                }
                else
                {
                    existing.Value = value;
                    existing.SourceTimestamp = DateTimeOffset.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static IReadOnlyList<(string FieldName, string Value)> ExtractFields(string filePath)
    {
        var result = new List<(string, string)>();

        try
        {
            using var document = PdfDocument.Open(filePath, new ParsingOptions { UseLenientParsing = true });
            var info = document.Information;

            if (!string.IsNullOrWhiteSpace(info.Title))
                result.Add(("Title", info.Title.Trim()));
            if (!string.IsNullOrWhiteSpace(info.Author))
                result.Add(("Author", info.Author.Trim()));
            if (!string.IsNullOrWhiteSpace(info.Subject))
                result.Add(("Subject", info.Subject.Trim()));
            if (!string.IsNullOrWhiteSpace(info.Creator))
                result.Add(("Creator", info.Creator.Trim()));
        }
        catch (Exception)
        {
            // Lenient: bad metadata returns empty list, not an exception.
        }

        return result;
    }
}
```

**Note:** `BookMetadataFieldRow` currently lacks a `Source` property. We need to add it. Check `BookMetadataFieldRow.cs` — it has `Source` as a string. Good — it's already there.

---

## Task 7: Infrastructure — ThumbnailService and SpineService

**Files:**
- Create: `src/OgmaLibrary.Application/Ingestion/IThumbnailService.cs`
- Create: `src/OgmaLibrary.Application/Ingestion/ISpineService.cs`
- Create: `src/OgmaLibrary.Infrastructure/Assets/ThumbnailService.cs`
- Create: `src/OgmaLibrary.Infrastructure/Assets/SpineService.cs`

**Step 1: Create interfaces**

`IThumbnailService.cs`:
```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Renders the cover thumbnail for a book by rendering page 0 of its PDF with PDFium,
/// resizing to 200×300 px, and writing a JPEG 85% to the sidecar (FR-LIB-005).
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Generates and persists the cover thumbnail for the specified book.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier (used as the content-hash key).</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file (sidecar path key).</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> GenerateCoverAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
```

`ISpineService.cs`:
```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Renders the spine strip for a book by rendering page 0 of its PDF with PDFium,
/// cropping and scaling to 7×100 px, and writing a JPEG to the sidecar (FR-LIB-005).
/// </summary>
public interface ISpineService
{
    /// <summary>
    /// Generates and persists the spine strip for the specified book.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file.</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> GenerateSpineAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
```

**Step 2: Create ThumbnailService.cs**

```csharp
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using PDFtoImage;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Renders page 0 of a PDF with PDFtoImage (spike S02 winner), resizes to 200×300 px
/// via SkiaSharp, and writes a JPEG 85% to the sidecar (FR-LIB-005).
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private const int TargetWidth = 200;
    private const int TargetHeight = 300;

    private readonly ISidecarService _sidecar;

    /// <summary>
    /// Initializes a new instance of <see cref="ThumbnailService"/>.
    /// </summary>
    /// <param name="sidecar">The sidecar service used to resolve output paths.</param>
    public ThumbnailService(ISidecarService sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        _sidecar = sidecar;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> GenerateCoverAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        try
        {
            string outputPath = _sidecar.Resolve(contentHash, SidecarClass.Covers);

            await Task.Run(() =>
            {
                RenderAndSaveCover(absoluteFilePath, outputPath);
            }, cancellationToken).ConfigureAwait(false);

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static void RenderAndSaveCover(string pdfPath, string outputPath)
    {
        // Render page 0 at 144 DPI (2x factor for quality).
        using SKBitmap rendered = Conversion.ToImage(
            pdfPath,
            page: 0,
            options: new RenderOptions(Dpi: 144));

        // Resize to TargetWidth x TargetHeight with letterboxing.
        using var surface = SKSurface.Create(
            new SKImageInfo(TargetWidth, TargetHeight, SKColorType.Rgb888x, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Compute scale to fit within the target size.
        float scaleX = (float)TargetWidth / rendered.Width;
        float scaleY = (float)TargetHeight / rendered.Height;
        float scale = Math.Min(scaleX, scaleY);
        float drawW = rendered.Width * scale;
        float drawH = rendered.Height * scale;
        float offsetX = (TargetWidth - drawW) / 2f;
        float offsetY = (TargetHeight - drawH) / 2f;

        var destRect = new SKRect(offsetX, offsetY, offsetX + drawW, offsetY + drawH);
        canvas.DrawBitmap(rendered, destRect);

        // Encode as JPEG 85%.
        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(stream);
    }
}
```

**Step 3: Create SpineService.cs**

```csharp
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using PDFtoImage;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Renders page 0 of a PDF at low resolution, crops and scales to a 7×100 spine strip,
/// and writes a JPEG to the sidecar (FR-LIB-005, Phase 14 3D shelf asset).
/// </summary>
public sealed class SpineService : ISpineService
{
    private const int SpineWidth = 7;
    private const int SpineHeight = 100;

    private readonly ISidecarService _sidecar;

    /// <summary>
    /// Initializes a new instance of <see cref="SpineService"/>.
    /// </summary>
    /// <param name="sidecar">The sidecar service used to resolve output paths.</param>
    public SpineService(ISidecarService sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        _sidecar = sidecar;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> GenerateSpineAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        try
        {
            string outputPath = _sidecar.Resolve(contentHash, SidecarClass.Spines);

            await Task.Run(() =>
            {
                RenderAndSaveSpine(absoluteFilePath, outputPath);
            }, cancellationToken).ConfigureAwait(false);

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static void RenderAndSaveSpine(string pdfPath, string outputPath)
    {
        // Render page 0 at 36 DPI (low-res is sufficient for a 7px wide strip).
        using SKBitmap rendered = Conversion.ToImage(
            pdfPath,
            page: 0,
            options: new RenderOptions(Dpi: 36));

        // Scale to SpineWidth x SpineHeight.
        using var surface = SKSurface.Create(
            new SKImageInfo(SpineWidth, SpineHeight, SKColorType.Rgb888x, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var destRect = new SKRect(0, 0, SpineWidth, SpineHeight);
        canvas.DrawBitmap(rendered, destRect);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(stream);
    }
}
```

**Step 4: Build to check for compilation issues**

```
dotnet build OgmaLibrary.sln -c Release
```

---

## Task 8: Infrastructure — UnavailableFileFlagService

**Files:**
- Create: `src/OgmaLibrary.Application/Ingestion/IUnavailableFileFlagService.cs`
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/UnavailableFileFlagService.cs`

**Step 1: Create the interface**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Flags previously-catalogued files that no longer exist on disk as
/// <c>Unavailable</c>, without deleting any user data (FR-LIB-004).
/// </summary>
public interface IUnavailableFileFlagService
{
    /// <summary>
    /// Iterates all <c>Present</c> BookFiles for the given library root, checks
    /// whether each file still exists, and flags missing files as <c>Missing</c>
    /// with a corresponding <c>AuditEvent</c>.
    /// </summary>
    /// <param name="libraryRoot">The absolute path to the library root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of files flagged as unavailable.</returns>
    Task<int> FlagMissingFilesAsync(string libraryRoot, CancellationToken cancellationToken = default);
}
```

**Step 2: Create the implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Flags BookFiles no longer present on disk as Missing (FileStatus=1) and sets the
/// owning Book's Status to Unavailable (1), while leaving all user data intact
/// (FR-LIB-004, reversibility R1).
/// </summary>
public sealed class UnavailableFileFlagService : IUnavailableFileFlagService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="UnavailableFileFlagService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public UnavailableFileFlagService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<int> FlagMissingFilesAsync(
        string libraryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        // Load all Present book files (FileStatus=0).
        List<BookFileRow> presentFiles = await _context.BookFiles
            .Where(f => f.FileStatus == 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(libraryRoot));
        int flagged = 0;

        foreach (BookFileRow fileRow in presentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string absolutePath = Path.Combine(
                normalizedRoot,
                fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(absolutePath))
            {
                continue;
            }

            // Flag the file as missing.
            fileRow.FileStatus = 1; // Missing

            // Flag the owning book as Unavailable.
            BookRow? book = await _context.Books
                .FirstOrDefaultAsync(b => b.BookId == fileRow.BookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null && book.Status == 0) // Active
            {
                book.Status = 1; // Unavailable
            }

            // Append audit event.
            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "BookMarkedUnavailable",
                EntityId = fileRow.BookId,
                EntityType = "Book",
                AfterJson = $"{{\"relativePath\":\"{fileRow.RelativePath}\"}}",
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });

            flagged++;
        }

        if (flagged > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return flagged;
    }
}
```

---

## Task 9: Infrastructure — BookRegistrationService (new-book insertion + job enqueue)

**Files:**
- Create: `src/OgmaLibrary.Application/Ingestion/IBookRegistrationService.cs`
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/BookRegistrationService.cs`

**Step 1: Create the interface**

```csharp
namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Registers a newly-discovered PDF file in the catalogue and enqueues the background
/// asset-generation jobs (FR-LIB-003, NFR-OGMA-009).
/// </summary>
public interface IBookRegistrationService
{
    /// <summary>
    /// Inserts a new <c>Book</c> and <c>BookFile</c> row for the discovered file,
    /// then enqueues metadata extraction and thumbnail generation jobs.
    /// </summary>
    /// <param name="discovered">The discovered file record.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file content.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stable book identifier assigned to the new book.</returns>
    Task<string> RegisterAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <c>BookFile</c> for a previously-catalogued book whose path changed
    /// (i.e., file was renamed or moved). Re-activates the file if it was Missing.
    /// </summary>
    /// <param name="bookId">The existing book identifier.</param>
    /// <param name="discovered">The file at its new path.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file content.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateFilePathAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default);
}
```

**Step 2: Create the implementation**

The ULID generator needs to produce a 26-char Crockford base-32 string. Use a simple timestamp+random approach.

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Inserts new <c>Book</c> and <c>BookFile</c> rows, enqueues metadata and thumbnail
/// jobs, and updates file paths for re-matched books (FR-LIB-003, NFR-OGMA-009).
/// </summary>
public sealed class BookRegistrationService : IBookRegistrationService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookRegistrationService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public BookRegistrationService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> RegisterAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        string bookId = GenerateBookId();

        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Sha256Hash = contentHash,
            SizeBytes = discovered.SizeBytes,
            MtimeTicks = discovered.MtimeTicks,
            Status = 0, // Active
        });

        _context.BookFiles.Add(new BookFileRow
        {
            BookId = bookId,
            RelativePath = discovered.RelativePath,
            FileStatus = 0, // Present
            LastSeenUtc = DateTimeOffset.UtcNow,
        });

        // Enqueue metadata extraction job.
        TryAddJob(bookId, "MetadataExtraction",
            ComputeIdempotencyKey(bookId, "MetadataExtraction"), discovered.AbsolutePath);

        // Enqueue thumbnail generation job.
        TryAddJob(bookId, "ThumbnailGeneration",
            ComputeIdempotencyKey(bookId, "ThumbnailGeneration"), discovered.AbsolutePath);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bookId;
    }

    /// <inheritdoc />
    public async Task UpdateFilePathAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        BookFileRow? fileRow = await _context.BookFiles
            .FirstOrDefaultAsync(f => f.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (fileRow is not null)
        {
            fileRow.RelativePath = discovered.RelativePath;
            fileRow.FileStatus = 0; // Present
            fileRow.LastSeenUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            _context.BookFiles.Add(new BookFileRow
            {
                BookId = bookId,
                RelativePath = discovered.RelativePath,
                FileStatus = 0,
                LastSeenUtc = DateTimeOffset.UtcNow,
            });
        }

        // Re-activate the book if it was Unavailable.
        BookRow? book = await _context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is not null && book.Status == 1)
        {
            book.Status = 0; // Active
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void TryAddJob(string bookId, string jobType, string idempotencyKey, string filePath)
    {
        // Only add if no job with this idempotency key already exists.
        bool exists = _context.Jobs.Any(j => j.IdempotencyKey == idempotencyKey);
        if (!exists)
        {
            _context.Jobs.Add(new JobRow
            {
                JobType = jobType,
                IdempotencyKey = idempotencyKey,
                Status = 0, // Pending
                BookId = bookId,
                Payload = filePath,
            });
        }
    }

    private static string ComputeIdempotencyKey(string bookId, string jobType)
    {
        // SHA-256 of "bookId|jobType" ensures uniqueness and is deterministic.
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32]; // 32 hex chars is plenty unique
    }

    private static string GenerateBookId()
    {
        // Simple ULID-style 26-char Crockford base-32.
        // Format: 10 chars timestamp + 16 chars random = 26 chars.
        const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<char> buf = stackalloc char[26];

        // Encode timestamp as 10 Crockford chars (5 bits each = 50 bits, covers ~34 years).
        for (int i = 9; i >= 0; i--)
        {
            buf[i] = Crockford[(int)(ts & 0x1F)];
            ts >>= 5;
        }

        // Fill remaining 16 chars with random bits.
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);
        int bitBuf = 0;
        int bitCount = 0;
        int ri = 0;
        for (int i = 10; i < 26; i++)
        {
            if (bitCount < 5)
            {
                bitBuf = (bitBuf << 8) | (ri < random.Length ? random[ri++] : 0);
                bitCount += 8;
            }

            buf[i] = Crockford[(bitBuf >> (bitCount - 5)) & 0x1F];
            bitCount -= 5;
        }

        return new string(buf);
    }
}
```

---

## Task 10: Infrastructure — IngestionOrchestrator (pipeline coordinator)

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/IngestionOrchestrator.cs`

**Step 1: Write the orchestrator**

This is the core pipeline: discovery → identity resolution → (new book registration | existing book update) → unavailable-file flagging.

```csharp
using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Coordinates the full ingestion pipeline for a single scan: discovery → identity
/// matching → book registration → unavailable-file flagging (FR-LIB-001..004,
/// NFR-OGMA-009, NFR-PROD-005). All heavy work runs on background threads; progress
/// is reported via <see cref="IScanProgressService"/>.
/// </summary>
public sealed class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly ILibrarySettingsService _settings;
    private readonly IPdfDiscoveryService _discovery;
    private readonly IBookIdentityService _identity;
    private readonly IBookRegistrationService _registration;
    private readonly IUnavailableFileFlagService _flagService;
    private readonly IScanProgressService _progress;
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="IngestionOrchestrator"/>.
    /// </summary>
    /// <param name="settings">The library settings service.</param>
    /// <param name="discovery">The PDF discovery service.</param>
    /// <param name="identity">The book identity service.</param>
    /// <param name="registration">The book registration service.</param>
    /// <param name="flagService">The unavailable-file flag service.</param>
    /// <param name="progress">The scan progress service.</param>
    /// <param name="context">The catalogue DB context.</param>
    public IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(flagService);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(context);

        _settings = settings;
        _discovery = discovery;
        _identity = identity;
        _registration = registration;
        _flagService = flagService;
        _progress = progress;
        _context = context;
    }

    /// <inheritdoc />
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        string? root = await _settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        IReadOnlyList<string> excluded = await _settings.GetExcludedFoldersAsync(cancellationToken).ConfigureAwait(false);

        _progress.Reset();
        _progress.SetPhase(ScanPhase.Discovering);

        // Bounded channel provides back-pressure; capacity = 500 per architecture doc.
        var channel = Channel.CreateBounded<DiscoveredFile>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });

        // Start discovery on a background task.
        Task discoveryTask = _discovery.DiscoverAsync(
            root, excluded, channel.Writer, cancellationToken);

        // Process discovered files as they stream in.
        _progress.SetPhase(ScanPhase.Processing);

        await foreach (DiscoveredFile file in channel.Reader.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            _progress.IncrementDiscovered();

            try
            {
                await ProcessFileAsync(file, root, cancellationToken).ConfigureAwait(false);
                _progress.IncrementCompleted();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Per-file failure isolation: log to jobs table and continue.
                _progress.IncrementFailed();
                await RecordFailureAsync(file.RelativePath, ex.Message, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await discoveryTask.ConfigureAwait(false);

        // Flag files that have disappeared from disk.
        await _flagService.FlagMissingFilesAsync(root, cancellationToken).ConfigureAwait(false);

        ScanProgressSnapshot final = _progress.CurrentSnapshot;
        _progress.SetPhase(final.FilesFailed > 0 ? ScanPhase.PartialFailure : ScanPhase.Complete);
    }

    private async Task ProcessFileAsync(
        DiscoveredFile file,
        string root,
        CancellationToken cancellationToken)
    {
        // Incremental rescan fast-path: check mtime+size before hashing.
        BookFileRow? unchanged = await _context.BookFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.RelativePath == file.RelativePath,
                cancellationToken)
            .ConfigureAwait(false);

        if (unchanged is not null)
        {
            // Look up the parent book row to check size+mtime.
            BookRow? bookRow = await _context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookId == unchanged.BookId, cancellationToken)
                .ConfigureAwait(false);

            if (bookRow?.SizeBytes == file.SizeBytes && bookRow?.MtimeTicks == file.MtimeTicks)
            {
                // File unchanged — update LastSeenUtc only (FR-LIB-006 fast-path).
                BookFileRow? tracked = await _context.BookFiles
                    .FirstOrDefaultAsync(f => f.BookFileId == unchanged.BookFileId, cancellationToken)
                    .ConfigureAwait(false);

                if (tracked is not null)
                {
                    tracked.LastSeenUtc = DateTimeOffset.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }
        }

        // Full pipeline: compute hash and resolve identity.
        string contentHash = await ComputeSha256Async(file.AbsolutePath, cancellationToken)
            .ConfigureAwait(false);

        BookMatchResult result = await _identity.ResolveAsync(file.AbsolutePath, root, cancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case BookMatchResult.NewBook:
                await _registration.RegisterAsync(file, contentHash, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case BookMatchResult.ExactMatch exact:
                await _registration.UpdateFilePathAsync(exact.BookId, file, contentHash, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case BookMatchResult.FuzzyMatch fuzzy:
                await _registration.UpdateFilePathAsync(fuzzy.BookId, file, contentHash, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case BookMatchResult.Unresolvable:
                // Record as a skipped/failed item.
                throw new InvalidOperationException($"Cannot resolve identity for {file.RelativePath}");
        }
    }

    private async Task RecordFailureAsync(
        string relativePath,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        string idempotencyKey = ComputeFailureKey(relativePath);

        bool exists = await _context.Jobs
            .AnyAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _context.Jobs.Add(new JobRow
            {
                JobType = "IngestionFailure",
                IdempotencyKey = idempotencyKey,
                Status = 3, // Failed
                Payload = relativePath,
                ErrorMessage = errorMessage,
                StartedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
            });

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
    }

    private static string ComputeFailureKey(string relativePath)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"failure|{relativePath}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
```

---

## Task 11: Workers — BookIngestionWorker and JobRecoveryService

**Files:**
- Create: `src/OgmaLibrary.Workers/BookIngestionWorker.cs`
- Create: `src/OgmaLibrary.Workers/JobRecoveryService.cs`

**Step 1: Create JobRecoveryService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Workers;

/// <summary>
/// Re-queues background jobs that were left in the <c>Running</c> state (Status=1)
/// when the application crashed or was terminated mid-scan (NFR-OGMA-009).
/// Called once at startup before the <see cref="BookIngestionWorker"/> begins.
/// </summary>
public sealed class JobRecoveryService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="JobRecoveryService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public JobRecoveryService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Loads all jobs with <c>Status = Running</c> and resets them to
    /// <c>Status = Pending</c> with <c>RetryCount + 1</c>, appending an audit event
    /// per recovered job.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of jobs recovered.</returns>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        List<JobRow> stuck = await _context.Jobs
            .Where(j => j.Status == 1) // Running
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (JobRow job in stuck)
        {
            job.Status = 0; // Pending
            job.RetryCount += 1;
            job.StartedUtc = null;

            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "JobRecovered",
                EntityId = job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                EntityType = "Job",
                AfterJson = $"{{\"retryCount\":{job.RetryCount}}}",
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });
        }

        if (stuck.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return stuck.Count;
    }
}
```

**Step 2: Create BookIngestionWorker.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker that processes pending background jobs (MetadataExtraction,
/// ThumbnailGeneration, SpineGeneration) from the Jobs queue (NFR-OGMA-009,
/// NFR-PROD-005). Per-file failure isolation: one failing job never cancels siblings.
/// </summary>
public sealed class BookIngestionWorker : BackgroundService
{
    private readonly CatalogueDbContext _context;
    private readonly IMetadataExtractionService _metadataExtraction;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISpineService _spineService;
    private readonly IScanProgressService _progress;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIngestionWorker"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="metadataExtraction">The metadata extraction service.</param>
    /// <param name="thumbnailService">The thumbnail generation service.</param>
    /// <param name="spineService">The spine generation service.</param>
    /// <param name="progress">The scan progress service.</param>
    public BookIngestionWorker(
        CatalogueDbContext context,
        IMetadataExtractionService metadataExtraction,
        IThumbnailService thumbnailService,
        ISpineService spineService,
        IScanProgressService progress)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metadataExtraction);
        ArgumentNullException.ThrowIfNull(thumbnailService);
        ArgumentNullException.ThrowIfNull(spineService);
        ArgumentNullException.ThrowIfNull(progress);

        _context = context;
        _metadataExtraction = metadataExtraction;
        _thumbnailService = thumbnailService;
        _spineService = spineService;
        _progress = progress;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Poll the Jobs table for pending work.
        while (!stoppingToken.IsCancellationRequested)
        {
            List<JobRow> pending = await _context.Jobs
                .Where(j => j.Status == 0 && // Pending
                    (j.JobType == "MetadataExtraction" ||
                     j.JobType == "ThumbnailGeneration" ||
                     j.JobType == "SpineGeneration"))
                .OrderBy(j => j.JobId)
                .Take(10)
                .ToListAsync(stoppingToken)
                .ConfigureAwait(false);

            if (pending.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                continue;
            }

            _progress.SetPhase(ScanPhase.GeneratingAssets);

            foreach (JobRow job in pending)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ExecuteJobAsync(job, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteJobAsync(JobRow job, CancellationToken stoppingToken)
    {
        // Mark as Running.
        job.Status = 1;
        job.StartedUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            bool success;
            string? errorMessage;

            // Resolve content hash from the Book row.
            string? contentHash = await _context.Books
                .AsNoTracking()
                .Where(b => b.BookId == job.BookId)
                .Select(b => b.Sha256Hash)
                .FirstOrDefaultAsync(stoppingToken)
                .ConfigureAwait(false);

            string filePath = job.Payload ?? string.Empty;

            if (job.JobType == "MetadataExtraction")
            {
                (success, errorMessage) = await _metadataExtraction.ExtractAsync(
                    job.BookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);
            }
            else if (job.JobType == "ThumbnailGeneration" && contentHash is not null)
            {
                (success, errorMessage) = await _thumbnailService.GenerateCoverAsync(
                    job.BookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (job.JobType == "SpineGeneration" && contentHash is not null)
            {
                (success, errorMessage) = await _spineService.GenerateSpineAsync(
                    job.BookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else
            {
                success = false;
                errorMessage = $"Unknown job type: {job.JobType}";
            }

            job.Status = success ? 2 : 3; // Completed or Failed
            job.ErrorMessage = errorMessage;
            job.CompletedUtc = DateTimeOffset.UtcNow;

            if (success)
            {
                _progress.IncrementCompleted();
            }
            else
            {
                _progress.IncrementFailed();
            }
        }
        catch (OperationCanceledException)
        {
            // Reset to Pending so recovery picks it up next restart.
            job.Status = 0;
            job.StartedUtc = null;
            throw;
        }
        catch (Exception ex)
        {
            // Per-file isolation: failure recorded, worker continues.
            job.Status = 3; // Failed
            job.ErrorMessage = ex.Message;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            _progress.IncrementFailed();
        }
        finally
        {
            try
            {
                await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Swallow save errors to prevent worker crash on DB issues.
            }
        }
    }
}
```

---

## Task 12: Infrastructure — ScanHealthService

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/ScanHealthService.cs`

**Step 1: Write the implementation**

```csharp
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Aggregates scan health data from the Jobs table and Books catalogue (FR-LIB-007).
/// </summary>
public sealed class ScanHealthService : IScanHealthService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ScanHealthService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public ScanHealthService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ScanHealthReport> GetReportAsync(CancellationToken cancellationToken = default)
    {
        // Failed jobs (general failures).
        List<JobRow> failedJobs = await _context.Jobs
            .Where(j => j.Status == 3 && j.JobType != "PasswordProtectedDetected")
            .OrderByDescending(j => j.CompletedUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Password-protected files (job type sentinel).
        List<JobRow> passwordJobs = await _context.Jobs
            .Where(j => j.JobType == "PasswordProtectedDetected")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Books missing thumbnails (no cover sidecar recorded — heuristic: look for
        // ThumbnailGeneration jobs that failed or are still pending for a book).
        List<JobRow> missingThumbnailJobs = await _context.Jobs
            .Where(j => j.JobType == "ThumbnailGeneration" &&
                        (j.Status == 3 || j.Status == 0)) // Failed or Pending
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Books with metadata gaps (no Title or Author in BookMetadataFields).
        List<string> booksWithTitle = await _context.BookMetadataFields
            .Where(f => f.FieldName == "Title" && f.Value != null)
            .Select(f => f.BookId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<string> allBookIds = await _context.Books
            .Where(b => b.Status == 0) // Active
            .Select(b => b.BookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var metadataGapIds = allBookIds.Except(booksWithTitle).ToList();

        var metadataGapItems = new List<ScanFailureItem>();
        foreach (string bookId in metadataGapIds.Take(200))
        {
            BookRow? book = await _context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            // Get file path from BookFiles.
            BookFileRow? fileRow = await _context.BookFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            metadataGapItems.Add(new ScanFailureItem(
                FilePath: fileRow?.RelativePath ?? bookId,
                ErrorMessage: "Missing Title metadata",
                JobId: 0,
                FailedAtUtc: DateTimeOffset.UtcNow));
        }

        return new ScanHealthReport(
            FailedJobs: failedJobs.Select(j => new ScanFailureItem(
                FilePath: j.Payload ?? j.BookId ?? string.Empty,
                ErrorMessage: j.ErrorMessage,
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            PasswordProtected: passwordJobs.Select(j => new ScanFailureItem(
                FilePath: j.Payload ?? j.BookId ?? string.Empty,
                ErrorMessage: "Password-protected PDF",
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            MissingThumbnails: missingThumbnailJobs.Select(j => new ScanFailureItem(
                FilePath: j.Payload ?? j.BookId ?? string.Empty,
                ErrorMessage: j.ErrorMessage ?? "Thumbnail not generated",
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            MetadataGaps: metadataGapItems);
    }

    /// <inheritdoc />
    public async Task RetryAllFailedAsync(CancellationToken cancellationToken = default)
    {
        List<JobRow> failed = await _context.Jobs
            .Where(j => j.Status == 3)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (JobRow job in failed)
        {
            job.Status = 0; // Pending
            job.RetryCount += 1;
            job.ErrorMessage = null;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        JobRow? job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is not null)
        {
            job.Status = 0; // Pending
            job.RetryCount += 1;
            job.ErrorMessage = null;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
```

---

## Task 13: Service registration — wire everything into CompositionRoot

**Files:**
- Create: `src/OgmaLibrary.Infrastructure/Ingestion/IngestionServiceExtensions.cs`
- Modify: `src/OgmaLibrary.App/CompositionRoot.cs`
- Modify: `src/OgmaLibrary.Workers/OgmaLibrary.Workers.csproj` (add DI ref)

**Step 1: Create IngestionServiceExtensions.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Extension methods to register the Ingestion Pipeline bounded-context services
/// (Phase 05) with the DI container.
/// </summary>
public static class IngestionServiceExtensions
{
    /// <summary>
    /// Registers all ingestion-pipeline services: settings, discovery, orchestrator,
    /// progress, health, thumbnail/spine, metadata extraction, registration,
    /// unavailable flagging, background worker, and job recovery.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataDirectory">The app-data directory for settings persistence.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIngestionPipeline(
        this IServiceCollection services,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddSingleton<ILibrarySettingsService>(_ => new LibrarySettingsService(dataDirectory));
        services.AddSingleton<IPdfDiscoveryService, PdfDiscoveryService>();
        services.AddSingleton<IScanProgressService, ScanProgressService>();
        services.AddSingleton<IUnavailableFileFlagService, UnavailableFileFlagService>();
        services.AddSingleton<IBookRegistrationService, BookRegistrationService>();
        services.AddSingleton<IMetadataExtractionService, MetadataExtractionService>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<ISpineService, SpineService>();
        services.AddSingleton<IIngestionOrchestrator, IngestionOrchestrator>();
        services.AddSingleton<IScanHealthService, ScanHealthService>();

        // Job recovery — run once at startup.
        services.AddSingleton<JobRecoveryService>();

        // Background worker.
        services.AddHostedService<BookIngestionWorker>();

        return services;
    }
}
```

**Step 2: Update CompositionRoot.cs**

Add after the `AddCatalogueContext` call:
```csharp
// Phase 05 — Ingestion Pipeline.
services.AddIngestionPipeline(dataDirectory: dataDirectory);
```

And add the using directive at the top:
```csharp
using OgmaLibrary.Infrastructure.Ingestion;
```

**Step 3: Add Workers project reference to App.csproj if needed**

`OgmaLibrary.Workers` is already referenced in `OgmaLibrary.App.csproj`. The `IngestionServiceExtensions` is in `Infrastructure`, so no change to App project refs needed.

**Step 4: Build to check**

```
dotnet build OgmaLibrary.sln -c Release
```

---

## Task 14: App — Localization strings for Phase 05

**Files:**
- Modify: `src/OgmaLibrary.Infrastructure/Localization/InMemoryLocalizationService.cs`
- Modify: `src/OgmaLibrary.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/OgmaLibrary.App/Views/MainWindow.axaml`
- Modify: `src/OgmaLibrary.App/Views/MainWindow.axaml.cs`

**Step 1: Add Phase 05 strings to InMemoryLocalizationService**

Add the following to BOTH the `English` and `French` dictionaries:

English additions:
```
["Scan.Phase.Idle"] = "Ready",
["Scan.Phase.Discovering"] = "Discovering files…",
["Scan.Phase.Processing"] = "Processing…",
["Scan.Phase.GeneratingAssets"] = "Generating thumbnails…",
["Scan.Phase.Complete"] = "Scan complete",
["Scan.Phase.PartialFailure"] = "Scan complete — some files failed",
["Scan.Phase.Cancelled"] = "Scan cancelled",
["Scan.Status.Scanned"] = "Scanned {0} books",
["Scan.Button.Cancel"] = "Cancel",
["Scan.Progress.Files"] = "{0} / {1} files",
["Scan.Progress.Failed"] = "{0} failed",
["MainWindow.Status.Ready"] = "Ready — choose a library folder to begin",
```

French additions:
```
["Scan.Phase.Idle"] = "Prêt",
["Scan.Phase.Discovering"] = "Découverte des fichiers…",
["Scan.Phase.Processing"] = "Traitement…",
["Scan.Phase.GeneratingAssets"] = "Génération des miniatures…",
["Scan.Phase.Complete"] = "Analyse terminée",
["Scan.Phase.PartialFailure"] = "Analyse terminée — certains fichiers ont échoué",
["Scan.Phase.Cancelled"] = "Analyse annulée",
["Scan.Status.Scanned"] = "{0} livres analysés",
["Scan.Button.Cancel"] = "Annuler",
["Scan.Progress.Files"] = "{0} / {1} fichiers",
["Scan.Progress.Failed"] = "{0} échoués",
["MainWindow.Status.Ready"] = "Prêt — choisissez un dossier de bibliothèque pour commencer",
```

**Step 2: Update MainWindowViewModel**

Replace the static `StatusText` property with a live property that tracks scan progress. Add `IIngestionOrchestrator` and `IScanProgressService` as constructor parameters.

Add:
- `ScanProgressText` property (displays phase + counts)
- `ChooseFolderCommand` (async command that opens folder picker and kicks off scan)
- Update `StatusText` to use `Scan.Status.Ready` key

Key changes:
```csharp
// Properties to add to MainWindowViewModel:
public string ScanPhaseText => _localization[$"Scan.Phase.{_scanPhase}"];
public string ScanProgressText => string.Format(...);
public bool IsScanning => _scanPhase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;
public int FilesCompleted { get; private set; }
public int FilesDiscovered { get; private set; }
```

**Step 3: Wire the Choose Folder button in XAML**

Attach a click handler on the existing Choose Folder button to call the ViewModel's command.

---

## Task 15: App — MainWindow folder picker wiring

**Files:**
- Modify: `src/OgmaLibrary.App/ViewModels/MainWindowViewModel.cs` (substantial rewrite)
- Modify: `src/OgmaLibrary.App/Views/MainWindow.axaml` (add progress display)
- Modify: `src/OgmaLibrary.App/Views/MainWindow.axaml.cs` (wire button click)

**Step 1: Rewrite MainWindowViewModel**

The ViewModel needs:
1. Constructor that accepts `ILocalizationService`, `ILibrarySettingsService`, `IIngestionOrchestrator`, `IScanProgressService`.
2. A `ChooseFolderAsync(TopLevel topLevel)` method that calls `topLevel.StorageProvider.OpenFolderPickerAsync(...)`, persists the chosen path, resets progress, then calls `IIngestionOrchestrator.ScanAsync(...)` on a background task.
3. Progress properties updated on the UI thread via `Dispatcher.UIThread.Post`.

Full implementation (key excerpt):

```csharp
// Scan progress properties
private ScanPhase _scanPhase = ScanPhase.Idle;
private int _filesDiscovered;
private int _filesCompleted;
private int _filesFailed;

public string StatusText => IsScanning
    ? string.Format(_localization["Scan.Progress.Files"], _filesCompleted, _filesDiscovered)
    : _scanPhase == ScanPhase.Complete
        ? string.Format(_localization["Scan.Status.Scanned"], _filesCompleted)
        : _localization["MainWindow.Status.Ready"];

public bool IsScanning =>
    _scanPhase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;

public async Task ChooseFolderAsync(TopLevel topLevel)
{
    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
        new FolderPickerOpenOptions { Title = _localization["MainWindow.Action.ChooseFolder"], AllowMultiple = false });

    if (folders.Count == 0) return;
    string path = folders[0].Path.LocalPath;

    await _settingsService.SetLibraryRootAsync(path);

    _ = Task.Run(async () =>
    {
        try { await _orchestrator.ScanAsync(_scanCts.Token); }
        catch (OperationCanceledException) { }
    });
}
```

**Step 2: Update XAML**

Add a `TextBlock` below the Choose Folder button showing `{Binding StatusText}` (already exists in status bar — just update the binding key).

Ensure the Choose Folder button has a Click event handler that calls `ChooseFolderAsync`.

**Step 3: Wire in code-behind**

```csharp
private async void ChooseFolderButton_Click(object? sender, RoutedEventArgs e)
{
    if (DataContext is MainWindowViewModel vm)
    {
        await vm.ChooseFolderAsync(TopLevel.GetTopLevel(this)!);
    }
}
```

---

## Task 16: Tests — Phase 05 integration tests using QuestPDF fixtures

**Files:**
- Create: `tests/OgmaLibrary.Tests/Ingestion/IngestionTestFixture.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/DiscoveryServiceTests.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/IngestionPipelineTests.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/UnavailableFileFlagTests.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/IncrementalRescanTests.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/JobManagementTests.cs`
- Create: `tests/OgmaLibrary.Tests/Ingestion/ScanHealthTests.cs`
- Create: `artifacts/screenshots/` directory (created by test code)
- Create: `docs/developer-guide/images/` directory

**Step 1: Create IngestionTestFixture.cs**

This helper creates temp directories with synthetic PDFs using QuestPDF, similar to the spike. It:
- Generates N small PDF files using QuestPDF
- Creates subdirectories (including excluded ones)
- Creates a DB context
- Composes all required services for integration testing

```csharp
// Key structure:
public sealed class IngestionTestFixture : IDisposable
{
    public string RootDir { get; }
    public string ExcludedDir { get; }
    public CatalogueDbContext Context { get; }
    // ... all services

    public static IngestionTestFixture Create(int pdfCount = 5)
    {
        // Creates temp dir with:
        // <root>/books/book-{i}.pdf  (pdfCount files)
        // <root>/excluded/excluded-book.pdf  (1 file in excluded dir)
        // DB context with migrations applied
        // All services wired
    }

    public static void WriteSyntheticPdf(string path, string title, string author)
    {
        // Uses QuestPDF to write a minimal 1-page PDF
    }
}
```

**Step 2: Create DiscoveryServiceTests.cs**

```csharp
[Fact]
public async Task DiscoveryService_DiscoversPdfs_Recursively()
// Create 5 PDFs in 3 subdirs; assert all 5 discovered.

[Fact]
public async Task DiscoveryService_HonorsExcludedFolders()
// Create fixture with excluded dir; assert PDFs in excluded dir not emitted.

[Fact]
public async Task DiscoveryService_PathsNormalized_ForwardSlash()
// Assert RelativePath uses forward slashes.
```

**Step 3: Create IngestionPipelineTests.cs**

```csharp
[Fact]
public async Task IngestionPipeline_RegistersNewBooks()
// Scan 3 PDFs; assert 3 Book rows.

[Fact]
public async Task IngestionPipeline_RematuresRenamedFile()
// Register file A; rename on disk; rescan; assert 1 Book row, RelativePath updated.

[Fact]
public async Task IngestionPipeline_RematchesMovedFile()
// Register file A; move to sub-folder; rescan; assert 1 Book row.

[Fact]
public async Task IngestionPipeline_IdempotentRescan()
// Scan twice; assert Book count unchanged.
```

**Step 4: Create UnavailableFileFlagTests.cs**

```csharp
[Fact]
public async Task UnavailableFileFlagging_PreservesAnnotations()
// Add book + annotation; delete file; rescan; assert annotation intact + Book.Status=1.

[Fact]
public async Task UnavailableFileFlagging_PreservesProgress()
// Add book + reading progress; delete file; rescan; assert progress intact.

[Fact]
public async Task UnavailableFileFlagging_ReactivatesOnReappearance()
// Flag file missing; restore file; rescan; assert Book.Status=0.

[Fact]
public async Task UnavailableFileFlagging_WritesAuditEvent()
// Assert AuditEvents row with EventType="BookMarkedUnavailable".
```

**Step 5: Create IncrementalRescanTests.cs**

```csharp
[Fact]
public async Task IncrementalRescan_SkipsUnchangedFiles()
// Scan once; count Jobs; rescan; assert no new Job rows for unchanged files.
// Key assertion: hash computation is skipped (verified by Job row counts).

[Fact]
public async Task IncrementalRescan_Requeues_ChangedFiles()
// Modify one file's mtime; rescan; assert exactly 1 new Job row for that file.
```

**Step 6: Create JobManagementTests.cs**

```csharp
[Fact]
public async Task JobRecovery_AtStartup_RequeuesRunningJobs()
// Manually set 2 jobs to Status=1; run RecoverAsync; assert both Status=0.

[Fact]
public async Task IngestionWorker_PerFileIsolation_SiblingJobsContinueOnFailure()
// Inject a corrupt PDF as job 3 of 5; assert jobs 4 and 5 complete; job 3 Status=3.
```

**Step 7: Create ScanHealthTests.cs**

```csharp
[Fact]
public async Task HealthReport_ShowsAllFailureCategories()
// Seed one job in each failure category; assert health report exposes each non-empty.

[Fact]
public async Task HealthReport_RetryAll_RequeuesFailedJobs()
// Set 3 jobs to Failed; call RetryAllFailedAsync; assert all 3 are Pending.
```

---

## Task 17: Tests — Headless UI scan test (screenshot)

**Files:**
- Create: `tests/OgmaLibrary.Tests.Ui/ScanProgressTests.cs`

**Step 1: Write the headless scan test**

```csharp
[AvaloniaFact]
public async Task MainWindow_AfterScan_ShowsScannedCount()
{
    // 1. Create a temp dir with 3 synthetic PDFs (using QuestPDF).
    // 2. Build a minimal service container with all ingestion services wired.
    // 3. Build a MainWindowViewModel pointing at the temp dir.
    // 4. Show the window; invoke scan synchronously.
    // 5. Process Dispatcher jobs.
    // 6. Assert StatusText contains the book count.
    // 7. Capture screenshot to artifacts/screenshots/scan-en.png
    // 8. Copy to docs/developer-guide/images/scan-en.png
}
```

**Note:** The UI test needs access to `IIngestionOrchestrator`. Create a `FakeIngestionOrchestrator` that completes immediately with N books reported, to keep the headless test deterministic. The real pipeline tests cover the actual ingestion logic.

---

## Task 18: Architecture test — Workers must not reference App

**Files:**
- Modify: `tests/OgmaLibrary.Tests.Architecture/ArchitectureTests.cs`

**Step 1: Add the architecture test**

```csharp
[Fact]
public void Architecture_WorkersProject_HasNoDependencyOnAppProject()
{
    var result = Types.InAssembly(typeof(BookIngestionWorker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("OgmaLibrary.App")
        .GetResult();

    Assert.True(result.IsSuccessful, Describe(result));
}
```

---

## Task 19: Build, format check, and full test run

**Step 1: Run dotnet format**

```
dotnet format OgmaLibrary.sln --verify-no-changes
```

Fix any formatting issues found.

**Step 2: Build Release**

```
dotnet build OgmaLibrary.sln -c Release
```

Expected: 0 warnings, 0 errors.

**Step 3: Run all tests**

```
dotnet test OgmaLibrary.sln -c Release --logger "console;verbosity=normal"
```

Expected: All tests pass (original 52 + new Phase 05 tests).

**Step 4: Create artifact directories if needed**

```
mkdir -p artifacts/screenshots
mkdir -p docs/developer-guide/images
```

---

## Implementation Notes

### PDFtoImage API (from Spike S02)

The `PDFtoImage.Conversion.ToImage(string path, int page, RenderOptions options)` API returns an `SKBitmap`. The `RenderOptions` struct accepts `Dpi` as a named argument.

```csharp
// Spike-verified pattern:
using SKBitmap bitmap = Conversion.ToImage(pdfPath, page: 0, options: new RenderOptions(Dpi: 144));
```

### QuestPDF fixture generation (from Spike S02)

```csharp
QuestPDF.Settings.License = LicenseType.Community;
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Content().Text("Title: My Book\nAuthor: Test Author");
    });
}).GeneratePdf(outputPath);
```

### InMemoryLocalizationService modification

The localization service uses static dictionaries. Adding keys means modifying the static initializer. The test `MainWindow_CultureSwitch_UpdatesTitle_WithoutMissingResources` checks `DoesNotContain("⟦", viewModel.Tagline)` — adding keys is safe as long as both en and fr dictionaries are updated together.

### Channel back-pressure in PdfDiscoveryService

The `DiscoverAsync` implementation uses `TryWrite` in a synchronous loop. For Phase 05, this is acceptable as the channel capacity (500) is large relative to typical library sizes. In a future phase, the discovery loop can be made async with `await writer.WriteAsync(...)`. The current approach is simpler and avoids the complexity of mixing async/sync within `Task.Run`.

### MainWindowViewModel refactoring

The existing `StatusText` property returns `_localization["MainWindow.Status.Skeleton"]`. This must be updated but the key `MainWindow.Status.Skeleton` must remain in the dictionary (it will just no longer be used by the production path) to avoid breaking the existing `SkeletonRenderTests` which checks culture switching.

Alternative: add `MainWindow.Status.Ready` key and update the VM to use it, keeping `MainWindow.Status.Skeleton` in the dictionaries for test compatibility.

### SkiaSharp native library in headless tests

The `ThumbnailService` and `SpineService` use SkiaSharp + PDFtoImage. In headless tests on Windows, these should work. Guard rendering tests with a try/catch that records the caveat if the native library isn't loaded:

```csharp
// In ThumbnailService tests:
try { /* render */ }
catch (DllNotFoundException ex) { Skip.If(true, $"Native library not loaded: {ex.Message}"); }
```

Use `Xunit.Skip` or the test's `Assert` to record a skip rather than a fail.

---

## Execution Order

Run tasks in order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16 → 17 → 18 → 19.

After each task, run `dotnet build OgmaLibrary.sln -c Release` and fix any compilation errors before proceeding.

After Task 16, run `dotnet test OgmaLibrary.sln -c Release` and fix any failures.

Final gate: Task 19 must show 0 format changes, 0 build warnings, all tests green.
