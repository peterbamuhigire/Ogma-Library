using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Counts from one legacy asset migration pass.</summary>
/// <param name="Copied">Files copied into the app-data store and verified.</param>
/// <param name="Removed">Source files removed after hash verification.</param>
/// <param name="Kept">Files left in place because they could not be verified or conflicted.</param>
public sealed record LegacyAssetMigrationResult(int Copied, int Removed, int Kept);

/// <summary>
/// One-time move of derived assets from <c>&lt;library&gt;/.ogma/</c> into the
/// app-data asset store <c>&lt;DataDirectory&gt;/.ogma/</c> (Sept-23 Phase 05, T05.5,
/// D-04, K27). Each file is copied, its SHA-256 is verified against the source, and
/// only then is the source deleted. Only the derived-asset folders Ogma creates are
/// touched; write-back backups, write-back plans and any unknown file are left in
/// place, so a user's own files are never deleted.
/// </summary>
public sealed class LegacyAssetMigrationService
{
    /// <summary>Derived-asset class folders that are safe to move.</summary>
    public static readonly IReadOnlyList<string> DerivedAssetFolders =
        ["covers", "thumbnails", "spines", "ocr", "text", "embeddings", "citations", "export"];

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly string _storeRoot;
    private readonly ILogger _logger;

    /// <summary>Initializes the migration for the given app-data store.</summary>
    /// <param name="contextFactory">The catalogue context factory (audit log).</param>
    /// <param name="dataDirectory">The application data directory (asset store root).</param>
    /// <param name="logger">Optional logger.</param>
    public LegacyAssetMigrationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        string dataDirectory,
        ILogger<LegacyAssetMigrationService>? logger = null)
        : this(dataDirectory, logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>Test constructor using a direct context.</summary>
    internal LegacyAssetMigrationService(CatalogueDbContext context, string dataDirectory)
        : this(dataDirectory, null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    private LegacyAssetMigrationService(string dataDirectory, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _storeRoot = Path.GetFullPath(dataDirectory);
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <summary>
    /// Migrates legacy <c>.ogma</c> asset folders from every configured library root
    /// and any extra candidate folders (for example the legacy settings root).
    /// </summary>
    /// <param name="extraCandidates">Additional folders that may hold a legacy <c>.ogma</c> folder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<LegacyAssetMigrationResult> MigrateAsync(
        IEnumerable<string?> extraCandidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extraCandidates);
        var candidates = new List<(string Path, string? RootId)>();
        using (CatalogueContextLease lease = await CatalogueContextLease
                   .CreateAsync(_contextFactory, _context, cancellationToken)
                   .ConfigureAwait(false))
        {
            IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
                .LoadAsync(lease.Context, cancellationToken)
                .ConfigureAwait(false);
            candidates.AddRange(roots.Values.Select(root => (root.CanonicalPath, (string?)root.RootId)));
        }

        candidates.AddRange(extraCandidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => (path!, (string?)null)));

        int copied = 0;
        int removed = 0;
        int kept = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string path, string? rootId) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folder;
            try
            {
                folder = Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!seen.Add(folder) || string.Equals(
                    Path.TrimEndingDirectorySeparator(folder),
                    Path.TrimEndingDirectorySeparator(_storeRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LegacyAssetMigrationResult result = MigrateFolder(folder, cancellationToken);
            if (result.Copied + result.Removed + result.Kept == 0)
            {
                continue;
            }

            copied += result.Copied;
            removed += result.Removed;
            kept += result.Kept;
            await RecordAuditAsync(rootId, result, cancellationToken).ConfigureAwait(false);
        }

        if (copied + removed + kept > 0)
        {
            InfrastructureLog.LegacyAssetsMigrated(_logger, copied, removed, kept);
        }

        return new LegacyAssetMigrationResult(copied, removed, kept);
    }

    /// <summary>Migrates one library folder's <c>.ogma</c> derived assets.</summary>
    /// <param name="libraryFolder">The library folder that may contain <c>.ogma</c>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public LegacyAssetMigrationResult MigrateFolder(string libraryFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryFolder);
        string legacyRoot = Path.Combine(libraryFolder, ".ogma");
        if (!Directory.Exists(legacyRoot) || IsReparsePoint(legacyRoot))
        {
            return new LegacyAssetMigrationResult(0, 0, 0);
        }

        int copied = 0;
        int removed = 0;
        int kept = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };

        foreach (string assetClass in DerivedAssetFolders)
        {
            string sourceClassDir = Path.Combine(legacyRoot, assetClass);
            if (!Directory.Exists(sourceClassDir) || IsReparsePoint(sourceClassDir))
            {
                continue;
            }

            string targetClassDir = Path.Combine(_storeRoot, ".ogma", assetClass);
            foreach (string source in Directory.EnumerateFiles(sourceClassDir, "*", options).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(sourceClassDir, source);
                string target = Path.Combine(targetClassDir, relative);
                try
                {
                    MoveVerified(source, target, ref copied, ref removed, ref kept);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A read-only, offline or locked source stays where it is.
                    InfrastructureLog.BestEffortStepFailed(
                        _logger, ex, nameof(LegacyAssetMigrationService), "assets.migrate_file");
                    kept++;
                }
            }

            RemoveEmptyDirectories(sourceClassDir);
        }

        TryDeleteEmptyDirectory(legacyRoot);
        return new LegacyAssetMigrationResult(copied, removed, kept);
    }

    private static void MoveVerified(
        string source,
        string target,
        ref int copied,
        ref int removed,
        ref int kept)
    {
        byte[] sourceHash = HashFile(source);
        if (File.Exists(target))
        {
            if (sourceHash.AsSpan().SequenceEqual(HashFile(target)))
            {
                File.Delete(source);
                removed++;
            }
            else
            {
                // Same key, different bytes: never overwrite; leave the source for review.
                kept++;
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + ".migrating";
        File.Copy(source, temporary, overwrite: true);
        if (!sourceHash.AsSpan().SequenceEqual(HashFile(temporary)))
        {
            File.Delete(temporary);
            kept++;
            return;
        }

        File.Move(temporary, target);
        copied++;
        File.Delete(source);
        removed++;
    }

    private static byte[] HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private static void RemoveEmptyDirectories(string directory)
    {
        if (!Directory.Exists(directory) || IsReparsePoint(directory))
        {
            return;
        }

        foreach (string child in Directory.EnumerateDirectories(directory).ToList())
        {
            RemoveEmptyDirectories(child);
        }

        TryDeleteEmptyDirectory(directory);
    }

    private static void TryDeleteEmptyDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, recursive: false);
            }
        }
        catch (IOException)
        {
            // Intentionally ignored: a directory that is not empty or is in use stays.
        }
        catch (UnauthorizedAccessException)
        {
            // Intentionally ignored: a read-only folder stays; nothing is lost.
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private async Task RecordAuditAsync(
        string? rootId,
        LegacyAssetMigrationResult result,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        lease.Context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "LegacyAssetsMigrated",
            EntityId = rootId,
            EntityType = "LibraryRoot",
            AfterJson = $"{{\"copied\":{result.Copied},\"removed\":{result.Removed},\"kept\":{result.Kept}}}",
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
