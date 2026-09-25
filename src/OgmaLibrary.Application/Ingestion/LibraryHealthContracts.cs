using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Discovery-time validity of one PDF file (Sept-23 Phase 05, K21). Values are
/// persisted in <c>BookFiles.FileValidity</c> and <c>FileIssues.Reason</c>.
/// </summary>
public enum FileValidity
{
    /// <summary>The file looks like a complete PDF.</summary>
    Valid = 0,

    /// <summary>The file is zero bytes long.</summary>
    Empty = 1,

    /// <summary>The file does not start with a PDF signature.</summary>
    NotAPdf = 2,

    /// <summary>The file is truncated or structurally damaged.</summary>
    Damaged = 3,

    /// <summary>The file is a valid PDF that requires a password to open.</summary>
    Locked = 4,
}

/// <summary>Helpers for <see cref="FileValidity"/>.</summary>
public static class FileValidityExtensions
{
    /// <summary>Whether the file may appear in the catalogue as a book (valid or locked).</summary>
    public static bool IsCataloguable(this FileValidity validity) =>
        validity is FileValidity.Valid or FileValidity.Locked;
}

/// <summary>Raised when a directly opened file is empty or is not a PDF (Sept-23 Phase 05).</summary>
public sealed class InvalidPdfFileException : InvalidOperationException
{
    /// <summary>Initializes the exception with the detected validity.</summary>
    /// <param name="validity">Why the file cannot be opened.</param>
    public InvalidPdfFileException(FileValidity validity)
        : base($"The selected file cannot be opened as a PDF ({validity}).")
    {
        Validity = validity;
    }

    /// <summary>Why the file cannot be opened.</summary>
    public FileValidity Validity { get; }
}

/// <summary>Classifies a file's PDF validity from bounded byte reads.</summary>
public interface IPdfFileValidityClassifier
{
    /// <summary>Classifies one file without loading it into a PDF engine.</summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<FileValidity> ClassifyAsync(string absolutePath, CancellationToken cancellationToken = default);
}

/// <summary>Terminal outcome of one library scan (Sept-23 Phase 05, T05.9).</summary>
public enum ScanOutcome
{
    /// <summary>Every root scanned and every file was handled.</summary>
    Completed = 0,

    /// <summary>The scan finished, but some files need attention or failed.</summary>
    CompletedWithIssues = 1,

    /// <summary>The user or the application cancelled the scan.</summary>
    Cancelled = 2,

    /// <summary>The scan stopped because of an unexpected error.</summary>
    Failed = 3,
}

/// <summary>Counts reported when a scan reaches a terminal state.</summary>
/// <param name="Outcome">The terminal outcome.</param>
/// <param name="Added">New books registered.</param>
/// <param name="Updated">Existing books whose file changed or moved.</param>
/// <param name="NeedsAttention">Files that were not catalogued (empty, not a PDF, damaged).</param>
/// <param name="Missing">Tracked files flagged missing by this scan.</param>
/// <param name="Restored">Tracked files that reappeared and were marked available again.</param>
/// <param name="Failed">Files whose processing failed unexpectedly.</param>
/// <param name="RootsScanned">Library folders that were scanned.</param>
/// <param name="RootsOffline">Enabled library folders that could not be reached.</param>
public sealed record ScanSummary(
    ScanOutcome Outcome,
    int Added,
    int Updated,
    int NeedsAttention,
    int Missing,
    int Restored,
    int Failed,
    int RootsScanned,
    int RootsOffline)
{
    /// <summary>An empty summary with the given outcome.</summary>
    public static ScanSummary Empty(ScanOutcome outcome) => new(outcome, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>One file that was not catalogued and needs the user's attention.</summary>
/// <param name="IssueId">Stable issue identifier.</param>
/// <param name="RootId">The owning library folder, when known.</param>
/// <param name="RootName">Display name of the owning library folder.</param>
/// <param name="FileName">The file name (no directory).</param>
/// <param name="RelativePath">Root-relative, forward-slash path shown as secondary text.</param>
/// <param name="Reason">Why the file was not catalogued.</param>
/// <param name="DetectedUtc">When the problem was first detected.</param>
public sealed record NeedsAttentionItem(
    long IssueId,
    LibraryRootId? RootId,
    string RootName,
    string FileName,
    string RelativePath,
    FileValidity Reason,
    DateTimeOffset DetectedUtc);

/// <summary>Lists and resolves files that need attention (Sept-23 Phase 05, T05.6).</summary>
public interface ILibraryAttentionService
{
    /// <summary>Lists unresolved, non-ignored issues in enabled library folders.</summary>
    Task<IReadOnlyList<NeedsAttentionItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Counts unresolved, non-ignored issues in enabled library folders.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-checks one file now. Returns true when it is valid and was queued for cataloguing.</summary>
    Task<bool> RetryAsync(long issueId, CancellationToken cancellationToken = default);

    /// <summary>Hides one issue until the file changes.</summary>
    Task IgnoreAsync(long issueId, CancellationToken cancellationToken = default);

    /// <summary>Returns the absolute path of the issue's file, or null when it can no longer be located.</summary>
    Task<string?> GetAbsolutePathAsync(long issueId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns scanning: rescans, the deferred startup scan and per-root file watchers
/// (Sept-23 Phase 05, T05.8). Scans are single-flight; every scan ends in a
/// terminal <see cref="ScanOutcome"/>.
/// </summary>
public interface ILibraryMonitor
{
    /// <summary>Raised after a scan reaches a terminal state.</summary>
    event EventHandler<ScanSummary>? ScanCompleted;

    /// <summary>Whether a scan is running now.</summary>
    bool IsScanning { get; }

    /// <summary>The summary of the latest finished scan, if any.</summary>
    ScanSummary? LastSummary { get; }

    /// <summary>Scans all enabled roots, or only the given roots. Waits for any running scan first.</summary>
    Task<ScanSummary> RescanAsync(
        IReadOnlyCollection<LibraryRootId>? roots = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the running scan, if any.</summary>
    void CancelScan();

    /// <summary>
    /// Starts the monitor: migrates legacy settings and assets, schedules the
    /// startup scan after <paramref name="startupDelay"/>, and watches enabled roots.
    /// </summary>
    Task StartAsync(TimeSpan startupDelay, CancellationToken cancellationToken = default);

    /// <summary>Re-reads the configured roots and restarts file watchers.</summary>
    Task RefreshWatchersAsync(CancellationToken cancellationToken = default);
}
