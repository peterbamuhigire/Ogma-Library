using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Coordinates the full ingestion pipeline for a scan: discovery → validity →
/// identity matching → book registration → unavailable-file flagging (FR-LIB-001..004,
/// NFR-OGMA-009, NFR-PROD-005). Sept-23 Phase 05: every enabled library root is
/// scanned through its own root id, invalid files go to <c>FileIssues</c> instead of
/// the catalogue, reappearing files are restored, and every scan ends in a terminal
/// phase. All heavy work runs on background threads; progress is reported via
/// <see cref="IScanProgressService"/>.
/// </summary>
public sealed class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly ILibrarySettingsService _settings;
    private readonly IPdfDiscoveryService _discovery;
    private readonly IBookIdentityService _identity;
    private readonly IBookRegistrationService _registration;
    private readonly IUnavailableFileFlagService _flagService;
    private readonly IScanProgressService _progress;
    private readonly IPdfFileValidityClassifier _validity;
    private readonly ILogger _logger;
    private readonly CatalogueMigrator? _migrator;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

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
    /// <param name="migrator">Optional schema migrator used to repair startup-damaged catalogues.</param>
    /// <param name="validity">Optional validity classifier (defaults to the byte-level classifier).</param>
    internal IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        CatalogueDbContext context,
        CatalogueMigrator? migrator = null,
        IPdfFileValidityClassifier? validity = null)
        : this(settings, discovery, identity, registration, flagService, progress, migrator, validity, null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IngestionOrchestrator"/>.
    /// </summary>
    /// <param name="settings">The library settings service.</param>
    /// <param name="discovery">The PDF discovery service.</param>
    /// <param name="identity">The book identity service.</param>
    /// <param name="registration">The book registration service.</param>
    /// <param name="flagService">The unavailable-file flag service.</param>
    /// <param name="progress">The scan progress service.</param>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="migrator">Optional schema migrator used to repair startup-damaged catalogues.</param>
    /// <param name="validity">Optional validity classifier (defaults to the byte-level classifier).</param>
    /// <param name="logger">Optional logger.</param>
    public IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        IDbContextFactory<CatalogueDbContext> contextFactory,
        CatalogueMigrator? migrator = null,
        IPdfFileValidityClassifier? validity = null,
        ILogger<IngestionOrchestrator>? logger = null)
        : this(settings, discovery, identity, registration, flagService, progress, migrator, validity, logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    private IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        CatalogueMigrator? migrator,
        IPdfFileValidityClassifier? validity,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(flagService);
        ArgumentNullException.ThrowIfNull(progress);

        _settings = settings;
        _discovery = discovery;
        _identity = identity;
        _registration = registration;
        _flagService = flagService;
        _progress = progress;
        _migrator = migrator;
        _validity = validity ?? new PdfFileValidityClassifier();
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        ScanSummary summary = await ScanRootsAsync(null, cancellationToken).ConfigureAwait(false);
        if (summary.Outcome == ScanOutcome.Cancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<ScanSummary> ScanRootsAsync(
        IReadOnlyCollection<LibraryRootId>? roots,
        CancellationToken cancellationToken = default)
    {
        var tally = new ScanTally();
        var stopwatch = Stopwatch.StartNew();
        bool started = false;
        try
        {
            if (_migrator is not null)
            {
                await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<LibraryRootLocation> targets = await ResolveTargetsAsync(roots, cancellationToken)
                .ConfigureAwait(false);
            if (targets.Count == 0)
            {
                return ScanSummary.Empty(ScanOutcome.Completed);
            }

            IReadOnlyList<string> excluded = await _settings
                .GetExcludedFoldersAsync(cancellationToken)
                .ConfigureAwait(false);

            _progress.Reset();
            _progress.SetPhase(ScanPhase.Discovering);
            started = true;

            foreach (LibraryRootLocation root in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ScanRootAsync(root, excluded, tally, cancellationToken).ConfigureAwait(false);
            }

            ScanOutcome outcome = tally.Failed > 0 || tally.NeedsAttention > 0 || tally.RootsOffline > 0
                ? ScanOutcome.CompletedWithIssues
                : ScanOutcome.Completed;
            _progress.SetPhase(outcome == ScanOutcome.Completed ? ScanPhase.Complete : ScanPhase.PartialFailure);
            return Finish(tally, outcome, stopwatch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (started || tally.RootsScanned > 0)
            {
                _progress.SetPhase(ScanPhase.Cancelled);
            }

            return Finish(tally, ScanOutcome.Cancelled, stopwatch);
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            _progress.SetPhase(ScanPhase.Failed);
            InfrastructureLog.LibraryScanFailed(_logger, ex);
            throw;
        }
    }

    private ScanSummary Finish(ScanTally tally, ScanOutcome outcome, Stopwatch stopwatch)
    {
        ScanSummary summary = tally.ToSummary(outcome);
        InfrastructureLog.LibraryScanFinished(
            _logger,
            outcome,
            summary.Added,
            summary.Updated,
            summary.NeedsAttention,
            summary.Missing,
            summary.Restored,
            summary.RootsScanned,
            stopwatch.ElapsedMilliseconds);
        return summary;
    }

    private async Task<IReadOnlyList<LibraryRootLocation>> ResolveTargetsAsync(
        IReadOnlyCollection<LibraryRootId>? roots,
        CancellationToken cancellationToken)
    {
        string? legacyRoot = await _settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Existing single-root installs gain a root row and per-root file identity here
        // (idempotent; a no-op once every relative row has a root).
        await LibraryRootPaths.BackfillLegacyRootAsync(context, legacyRoot, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LibraryRootLocation> active = await LibraryRootPaths
            .GetActiveRootsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (roots is null)
        {
            return active;
        }

        var requested = roots.Select(root => root.Value).ToHashSet(StringComparer.Ordinal);
        return active.Where(root => requested.Contains(root.RootId)).ToList();
    }

    private async Task ScanRootAsync(
        LibraryRootLocation root,
        IReadOnlyList<string> excluded,
        ScanTally tally,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root.CanonicalPath))
        {
            // An unplugged drive or unreachable share: mark the root offline and keep
            // every book (never flag a whole folder missing because its volume is absent).
            tally.RootsOffline++;
            await SetRootStatusAsync(root.RootId, LibraryRootStatus.Unavailable, scanned: false, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        _progress.SetPhase(ScanPhase.Discovering);

        // Bounded channel provides back-pressure; capacity = 500 per architecture spec.
        var channel = Channel.CreateBounded<DiscoveredFile>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });
        int failedDirectories = 0;

        Task discoveryTask = _discovery.DiscoverAsync(
            root.CanonicalPath,
            excluded,
            channel.Writer,
            directoryDiagnosticSink: diagnostic =>
            {
                if (diagnostic.Status == DiscoveryDirectoryStatus.Failed)
                {
                    Interlocked.Increment(ref failedDirectories);
                }

                return ValueTask.CompletedTask;
            },
            cancellationToken: cancellationToken);

        _progress.SetPhase(ScanPhase.Processing);

        try
        {
            await foreach (DiscoveredFile file in channel.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                _progress.IncrementDiscovered();

                try
                {
                    await ProcessFileAsync(root, file, tally, cancellationToken).ConfigureAwait(false);
                    _progress.IncrementCompleted();
                }
                catch (Exception ex) when (ex is not OperationCanceledException &&
                                           !OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
                {
                    // Per-file failure isolation: record failure, continue with next file.
                    InfrastructureLog.DegradedStep(_logger, ex, nameof(IngestionOrchestrator), "scan.file");
                    tally.Failed++;
                    _progress.IncrementFailed();
                    await RecordFailureAsync(file.RelativePath, "File processing failed.", cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Never leave the discovery task unobserved, whatever ended the loop.
            try
            {
                await discoveryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Intentionally ignored: the caller observes the same cancellation.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Volatile.Read(ref failedDirectories) == 0)
        {
            // Flag files that have disappeared from disk (FR-LIB-004), for this root only.
            tally.Missing += await _flagService
                .FlagMissingFilesAsync(root.CanonicalPath, root.RootId, cancellationToken)
                .ConfigureAwait(false);
        }

        tally.RootsScanned++;
        await SetRootStatusAsync(root.RootId, LibraryRootStatus.Available, scanned: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProcessFileAsync(
        LibraryRootLocation root,
        DiscoveredFile discovered,
        ScanTally tally,
        CancellationToken cancellationToken)
    {
        FileValidity validity = await _validity.ClassifyAsync(discovered.AbsolutePath, cancellationToken)
            .ConfigureAwait(false);
        DiscoveredFile file = discovered with { LibraryRootId = root.RootId, Validity = validity };

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookFileRow? existing = await context.BookFiles
            .Where(f => f.LibraryRootId == root.RootId && f.RelativePath == file.RelativePath)
            .OrderBy(f => f.FileStatus)
            .ThenBy(f => f.BookFileId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!validity.IsCataloguable())
        {
            bool needsAttention = await RecordIssueAsync(context, root.RootId, file, validity, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null && existing.FileValidity != (int)validity)
            {
                // A tracked file became unreadable: hide it from the catalogue, keep its data.
                existing.FileValidity = (int)validity;
                existing.LastSeenUtc = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (needsAttention)
            {
                tally.NeedsAttention++;
            }

            return;
        }

        await ClearIssueAsync(context, root.RootId, file.RelativePath, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            BookRow? book = await context.Books
                .FirstOrDefaultAsync(b => b.BookId == existing.BookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null &&
                await IsUnchangedAsync(context, book, file, cancellationToken).ConfigureAwait(false))
            {
                // Incremental rescan fast path (FR-LIB-006): unchanged file.
                if (existing.FileStatus != 0)
                {
                    // T05.7: a tracked file that reappeared is available again.
                    existing.FileStatus = 0;
                    if (book.Status == 1)
                    {
                        book.Status = 0;
                    }

                    context.AuditEvents.Add(new AuditEventRow
                    {
                        EventType = "BookRestored",
                        EntityId = existing.BookId,
                        EntityType = "Book",
                        Timestamp = DateTimeOffset.UtcNow,
                        IsLocalOnly = true,
                    });
                    tally.Restored++;
                }

                existing.FileValidity = (int)validity;
                if (validity == FileValidity.Locked)
                {
                    book.IsPasswordProtected = true;
                }

                existing.LastSeenUtc = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            // Same path, changed content: the same book, re-identified.
            bool wasMissing = existing.FileStatus != 0;
            string changedHash = await ComputeSha256Async(file.AbsolutePath, cancellationToken)
                .ConfigureAwait(false);
            await _registration.UpdateFilePathAsync(existing.BookId, file, changedHash, cancellationToken)
                .ConfigureAwait(false);
            if (wasMissing)
            {
                tally.Restored++;
            }
            else
            {
                tally.Updated++;
            }

            return;
        }

        // Full pipeline: compute SHA-256 and resolve identity.
        string contentHash = await ComputeSha256Async(file.AbsolutePath, cancellationToken)
            .ConfigureAwait(false);

        BookMatchResult result = await _identity.ResolveAsync(file.AbsolutePath, root.CanonicalPath, cancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case BookMatchResult.NewBook:
                await _registration.RegisterAsync(file, contentHash, cancellationToken)
                    .ConfigureAwait(false);
                tally.Added++;
                break;

            case BookMatchResult.ExactMatch exact:
                await RegisterOrMoveAsync(exact.BookId, file, contentHash, context, tally, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case BookMatchResult.FuzzyMatch fuzzy:
                await RegisterOrMoveAsync(fuzzy.BookId, file, contentHash, context, tally, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case BookMatchResult.Unresolvable unresolvable:
                throw new InvalidOperationException(
                    $"Cannot resolve identity for {file.RelativePath}: {unresolvable.Reason}");
        }
    }

    /// <summary>
    /// Incremental fast path (FR-LIB-006). A book with several byte-identical occurrences
    /// stores one size/mtime, so for such books a differing mtime is settled by the content
    /// hash rather than re-identifying the book on every rescan (Sept-23 Phase 06, T06.9).
    /// </summary>
    private static async Task<bool> IsUnchangedAsync(
        CatalogueDbContext context,
        BookRow book,
        DiscoveredFile file,
        CancellationToken cancellationToken)
    {
        if (book.SizeBytes != file.SizeBytes)
        {
            return false;
        }

        if (book.MtimeTicks == file.MtimeTicks)
        {
            return true;
        }

        int occurrences = await context.BookFiles
            .CountAsync(f => f.BookId == book.BookId, cancellationToken)
            .ConfigureAwait(false);
        if (occurrences < 2 || string.IsNullOrWhiteSpace(book.Sha256Hash))
        {
            return false;
        }

        string hash = await ComputeSha256Async(file.AbsolutePath, cancellationToken).ConfigureAwait(false);
        return string.Equals(hash, book.Sha256Hash, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RegisterOrMoveAsync(
        string matchedBookId,
        DiscoveredFile file,
        string contentHash,
        CatalogueDbContext context,
        ScanTally tally,
        CancellationToken cancellationToken)
    {
        if (await ShouldRegisterMatchedFileAsNewBookAsync(matchedBookId, file, context, cancellationToken)
                .ConfigureAwait(false))
        {
            // Sept-23 Phase 06 (T06.9, K23): a byte-identical copy is another occurrence of the
            // same book, never a second book. Anything short of an exact content match stays a
            // separate book (a grouping proposal at most); books are never auto-merged.
            if (await TryAttachOccurrenceAsync(matchedBookId, file, contentHash, context, cancellationToken)
                    .ConfigureAwait(false))
            {
                tally.Updated++;
                return;
            }

            await _registration.RegisterAsync(file, contentHash, cancellationToken).ConfigureAwait(false);
            tally.Added++;
            return;
        }

        bool wasUnavailable = await context.Books
            .AsNoTracking()
            .AnyAsync(b => b.BookId == matchedBookId && b.Status == 1, cancellationToken)
            .ConfigureAwait(false);
        await _registration.UpdateFilePathAsync(matchedBookId, file, contentHash, cancellationToken)
            .ConfigureAwait(false);
        if (wasUnavailable)
        {
            tally.Restored++;
        }
        else
        {
            tally.Updated++;
        }
    }

    private static async Task<bool> TryAttachOccurrenceAsync(
        string matchedBookId,
        DiscoveredFile file,
        string contentHash,
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        BookRow? book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == matchedBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null ||
            !string.Equals(book.Sha256Hash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool alreadyTracked = await context.BookFiles
            .AnyAsync(
                f => f.BookId == matchedBookId &&
                     f.LibraryRootId == file.LibraryRootId &&
                     f.RelativePath == file.RelativePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!alreadyTracked)
        {
            context.BookFiles.Add(new BookFileRow
            {
                BookId = matchedBookId,
                LibraryRootId = file.LibraryRootId,
                RelativePath = file.RelativePath,
                FileStatus = 0,
                FileValidity = (int)file.Validity,
                LastSeenUtc = DateTimeOffset.UtcNow,
            });
            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "BookOccurrenceAttached",
                EntityId = matchedBookId,
                EntityType = "Book",
                AfterJson = "{\"reason\":\"identical_content\"}",
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<bool> ShouldRegisterMatchedFileAsNewBookAsync(
        string matchedBookId,
        DiscoveredFile discovered,
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        List<BookFileRow> presentFiles = await context.BookFiles
            .AsNoTracking()
            .Where(f => f.BookId == matchedBookId && f.FileStatus == 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (presentFiles.Count == 0)
        {
            return false;
        }

        string? legacyRoot = await _settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // A matched book that still has its own present file elsewhere is a copy, not a
        // move: the caller attaches an identical copy as an occurrence (Phase 06) and
        // registers anything else separately (duplicates UI is Phase 10).
        return presentFiles
            .Where(f => !(f.LibraryRootId == discovered.LibraryRootId &&
                          string.Equals(f.RelativePath, discovered.RelativePath, StringComparison.Ordinal)))
            .Select(f => LibraryRootPaths.Resolve(roots, f, legacyRoot))
            .Any(path => path is not null && File.Exists(path));
    }

    private static async Task<bool> RecordIssueAsync(
        CatalogueDbContext context,
        string rootId,
        DiscoveredFile file,
        FileValidity reason,
        CancellationToken cancellationToken)
    {
        FileIssueRow? issue = await context.FileIssues
            .FirstOrDefaultAsync(
                row => row.LibraryRootId == rootId && row.RelativePath == file.RelativePath,
                cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (issue is null)
        {
            context.FileIssues.Add(new FileIssueRow
            {
                LibraryRootId = rootId,
                RelativePath = file.RelativePath,
                Reason = (int)reason,
                SizeBytes = file.SizeBytes,
                MtimeTicks = file.MtimeTicks,
                DetectedUtc = now,
                LastCheckedUtc = now,
            });
            return true;
        }

        bool changed = issue.SizeBytes != file.SizeBytes || issue.MtimeTicks != file.MtimeTicks;
        if (changed)
        {
            // An ignored file that changed is worth a second look.
            issue.IsIgnored = false;
        }

        issue.Reason = (int)reason;
        issue.SizeBytes = file.SizeBytes;
        issue.MtimeTicks = file.MtimeTicks;
        issue.LastCheckedUtc = now;
        return !issue.IsIgnored;
    }

    private static async Task ClearIssueAsync(
        CatalogueDbContext context,
        string rootId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        FileIssueRow? issue = await context.FileIssues
            .FirstOrDefaultAsync(
                row => row.LibraryRootId == rootId && row.RelativePath == relativePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (issue is not null)
        {
            context.FileIssues.Remove(issue);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SetRootStatusAsync(
        string rootId,
        LibraryRootStatus status,
        bool scanned,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow? row = await context.LibraryRoots
            .FirstOrDefaultAsync(candidate => candidate.LibraryRootId == rootId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.RootStatus = (int)status;
        row.LastHealthCheckUtc = DateTimeOffset.UtcNow;
        if (scanned)
        {
            row.LastSuccessfulScanUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        string relativePath,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string idempotencyKey = ComputeFailureKey(relativePath);

        bool exists = await context.Jobs
            .AnyAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = "IngestionFailure",
                IdempotencyKey = idempotencyKey,
                Status = 3, // Failed
                Payload = relativePath,
                ErrorMessage = errorMessage,
                StartedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
    }

    private static string ComputeFailureKey(string relativePath)
    {
        byte[] data = Encoding.UTF8.GetBytes($"failure|{relativePath}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }

    private sealed class ScanTally
    {
        public int Added { get; set; }

        public int Updated { get; set; }

        public int NeedsAttention { get; set; }

        public int Missing { get; set; }

        public int Restored { get; set; }

        public int Failed { get; set; }

        public int RootsScanned { get; set; }

        public int RootsOffline { get; set; }

        public ScanSummary ToSummary(ScanOutcome outcome) => new(
            outcome,
            Added,
            Updated,
            NeedsAttention,
            Missing,
            Restored,
            Failed,
            RootsScanned,
            RootsOffline);
    }
}
