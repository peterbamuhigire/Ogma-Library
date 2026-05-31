using System.Security.Cryptography;
using System.Text;
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
    internal IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        CatalogueDbContext context,
        CatalogueMigrator? migrator = null)
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
        _migrator = migrator;
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
    public IngestionOrchestrator(
        ILibrarySettingsService settings,
        IPdfDiscoveryService discovery,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        IUnavailableFileFlagService flagService,
        IScanProgressService progress,
        IDbContextFactory<CatalogueDbContext> contextFactory,
        CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(flagService);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(contextFactory);

        _settings = settings;
        _discovery = discovery;
        _identity = identity;
        _registration = registration;
        _flagService = flagService;
        _progress = progress;
        _contextFactory = contextFactory;
        _migrator = migrator;
    }

    /// <inheritdoc />
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (_migrator is not null)
        {
            await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }

        string? root = await _settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        IReadOnlyList<string> excluded = await _settings
            .GetExcludedFoldersAsync(cancellationToken)
            .ConfigureAwait(false);

        _progress.Reset();
        _progress.SetPhase(ScanPhase.Discovering);

        // Bounded channel provides back-pressure; capacity = 500 per architecture spec.
        var channel = Channel.CreateBounded<DiscoveredFile>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });

        // Start discovery on a background task.
        Task discoveryTask = _discovery.DiscoverAsync(
            root, excluded, channel.Writer, cancellationToken);

        _progress.SetPhase(ScanPhase.Processing);

        // Process discovered files as they stream in.
        await foreach (DiscoveredFile file in channel.Reader
            .ReadAllAsync(cancellationToken)
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
                // Per-file failure isolation: record failure, continue with next file.
                _progress.IncrementFailed();
                await RecordFailureAsync(file.RelativePath, ex.Message, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await discoveryTask.ConfigureAwait(false);

        // Flag files that have disappeared from disk (FR-LIB-004).
        await _flagService.FlagMissingFilesAsync(root, cancellationToken).ConfigureAwait(false);

        ScanProgressSnapshot final = _progress.CurrentSnapshot;
        _progress.SetPhase(final.FilesFailed > 0 ? ScanPhase.PartialFailure : ScanPhase.Complete);
    }

    private async Task ProcessFileAsync(
        DiscoveredFile file,
        string root,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Incremental rescan fast-path: check size+mtime before hashing (FR-LIB-006).
        BookFileRow? existing = await context.BookFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.RelativePath == file.RelativePath,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            BookRow? bookRow = await context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookId == existing.BookId, cancellationToken)
                .ConfigureAwait(false);

            if (bookRow?.SizeBytes == file.SizeBytes && bookRow?.MtimeTicks == file.MtimeTicks)
            {
                // File unchanged — update LastSeenUtc only.
                BookFileRow? tracked = await context.BookFiles
                    .FirstOrDefaultAsync(f => f.BookFileId == existing.BookFileId, cancellationToken)
                    .ConfigureAwait(false);

                if (tracked is not null)
                {
                    tracked.LastSeenUtc = DateTimeOffset.UtcNow;
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }
        }

        // Full pipeline: compute SHA-256 and resolve identity.
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

            case BookMatchResult.Unresolvable unresolvable:
                throw new InvalidOperationException(
                    $"Cannot resolve identity for {file.RelativePath}: {unresolvable.Reason}");
        }
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
}
