using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Persists root-relative observations and queues changed files.</summary>
public sealed class IncrementalDiscoveryService : IIncrementalDiscoveryService
{
    private readonly ILibraryRootService _roots;
    private readonly IPdfDiscoveryService _discovery;
    private readonly IProcessingStateService _processing;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal IncrementalDiscoveryService(
        CatalogueDbContext context,
        ILibraryRootService roots,
        IPdfDiscoveryService discovery,
        IProcessingStateService processing)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _processing = processing ?? throw new ArgumentNullException(nameof(processing));
    }

    /// <summary>DI constructor using independent catalogue contexts.</summary>
    [ActivatorUtilitiesConstructor]
    public IncrementalDiscoveryService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibraryRootService roots,
        IPdfDiscoveryService discovery,
        IProcessingStateService processing,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _processing = processing ?? throw new ArgumentNullException(nameof(processing));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<DiscoveryScanResult> ScanAsync(
        LibraryRootId rootId,
        IReadOnlyList<string>? excludedFolders = null,
        CancellationToken cancellationToken = default)
    {
        LibraryRootDescriptor root = (await _roots.ListAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => candidate.Id == rootId)
            ?? throw new KeyNotFoundException($"Library root '{rootId.Value}' was not found.");
        if (!root.IsEnabled || root.Status != LibraryRootStatus.Available ||
            string.IsNullOrWhiteSpace(root.CanonicalLocator))
        {
            throw new InvalidOperationException("Discovery requires an enabled, available root with a locator.");
        }

        ScanSessionDescriptor session = await _processing
            .StartSessionAsync(rootId, cancellationToken)
            .ConfigureAwait(false);
        var channel = Channel.CreateBounded<DiscoveredFile>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });
        int seen = 0;
        int changed = 0;
        int unchanged = 0;
        int failed = 0;
        var diagnostics = new ConcurrentQueue<DiscoveryDirectoryDiagnostic>();
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        DirectoryCheckpointRow checkpoint = await GetCheckpointAsync(
            context, rootId, string.Empty, cancellationToken)
            .ConfigureAwait(false);
        string? resumeAfterDirectory = checkpoint.ScanState is 1 or 2
            ? checkpoint.ResumeCursorRelativeDirectory
            : null;
        checkpoint.LastStartedUtc = DateTimeOffset.UtcNow;
        checkpoint.LastScanSessionId = session.Id;
        checkpoint.ScanState = 1;
        checkpoint.LastErrorCode = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
        Task discoveryTask = _discovery.DiscoverAsync(
            root.CanonicalLocator,
            excludedFolders ?? [],
            channel.Writer,
            async diagnostic =>
            {
                diagnostics.Enqueue(diagnostic);
                if (_contextFactory is not null)
                {
                    await PersistDirectoryDiagnosticAsync(
                        rootId,
                        session.Id,
                        diagnostic,
                        cancellationToken).ConfigureAwait(false);
                }
            },
            resumeAfterDirectory,
            cancellationToken);

        try
        {
            await foreach (DiscoveredFile file in channel.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                seen++;
                string normalizedPath = NormalizeRelativePath(file.RelativePath);
                try
                {
                    DiscoveryObservationRow? observation = await context.DiscoveryObservations
                        .FirstOrDefaultAsync(row => row.LibraryRootId == rootId.Value &&
                                                    row.NormalizedRelativePath == normalizedPath,
                            cancellationToken)
                        .ConfigureAwait(false);
                    bool isChanged = observation is null ||
                        observation.SizeBytes != file.SizeBytes ||
                        observation.ModifiedUtcTicks != file.MtimeTicks;
                    if (observation is null)
                    {
                        observation = new DiscoveryObservationRow
                        {
                            LibraryRootId = rootId.Value,
                            NormalizedRelativePath = normalizedPath,
                            FirstSeenUtc = DateTimeOffset.UtcNow,
                        };
                        context.DiscoveryObservations.Add(observation);
                    }

                    observation.SizeBytes = file.SizeBytes;
                    observation.ModifiedUtcTicks = file.MtimeTicks;
                    observation.LastObservedScanSessionId = session.Id;
                    if (isChanged)
                    {
                        observation.Sha256Hash = await ComputeSha256Async(
                            file.AbsolutePath, cancellationToken).ConfigureAwait(false);
                    }
                    observation.LastSeenUtc = DateTimeOffset.UtcNow;
                    if (isChanged)
                    {
                        changed++;
                        await _processing.EnqueueStageForRootAsync(
                            rootId,
                            session.Id,
                            "FileProcessing",
                            BuildSubjectKey(rootId, normalizedPath, file.SizeBytes, file.MtimeTicks),
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        unchanged++;
                    }
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    failed++;
                }
            }

            await discoveryTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            checkpoint.ScanState = 2;
            checkpoint.LastCompletedUtc = DateTimeOffset.UtcNow;
            checkpoint.LastErrorCode = cancellationToken.IsCancellationRequested
                ? "scan_cancelled"
                : "discovery_scan_failed";
            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        DiscoveryDirectoryDiagnostic[] diagnosticSnapshot = diagnostics.ToArray();
        completedUtc = DateTimeOffset.UtcNow;
        foreach (DiscoveryDirectoryDiagnostic diagnostic in diagnosticSnapshot)
        {
            DirectoryCheckpointRow directoryCheckpoint = await GetCheckpointAsync(
                    context,
                    rootId,
                    diagnostic.RelativeDirectory,
                    cancellationToken)
                .ConfigureAwait(false);
            directoryCheckpoint.LastScanSessionId = session.Id;
            directoryCheckpoint.LastObservedFileCount = diagnostic.FilesSeen;
            directoryCheckpoint.LastErrorCode = diagnostic.ErrorCode;
            directoryCheckpoint.ScanState = diagnostic.Status == DiscoveryDirectoryStatus.Started
                ? 1
                : diagnostic.Status == DiscoveryDirectoryStatus.Completed ? 0 : 2;
            directoryCheckpoint.LastStartedUtc ??= diagnostic.OccurredUtc;
            if (diagnostic.Status == DiscoveryDirectoryStatus.Completed)
            {
                directoryCheckpoint.LastCompletedUtc = diagnostic.OccurredUtc;
                checkpoint.ResumeCursorRelativeDirectory = diagnostic.RelativeDirectory;
            }
        }
        bool directoryFailure = diagnosticSnapshot.Any(
            diagnostic => diagnostic.Status == DiscoveryDirectoryStatus.Failed);
        checkpoint.ScanState = directoryFailure ? 2 : 0;
        checkpoint.LastCompletedUtc = completedUtc;
        checkpoint.LastErrorCode = directoryFailure
            ? "directory_discovery_incomplete"
            : failed == 0 ? null : "observation_persist_failed";
        if (!directoryFailure)
        {
            checkpoint.ResumeCursorRelativeDirectory = null;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _roots.RecordSuccessfulScanAsync(rootId, cancellationToken).ConfigureAwait(false);
        return new DiscoveryScanResult(
            session,
            seen,
            changed,
            unchanged,
            failed,
            completedUtc,
            diagnosticSnapshot);
    }

    private static async Task<DirectoryCheckpointRow> GetCheckpointAsync(
        CatalogueDbContext context,
        LibraryRootId rootId,
        string relativeDirectory,
        CancellationToken cancellationToken)
    {
        DirectoryCheckpointRow? trackedCheckpoint = context.ChangeTracker
            .Entries<DirectoryCheckpointRow>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.LibraryRootId == rootId.Value &&
                                   row.NormalizedRelativeDirectory == relativeDirectory);
        if (trackedCheckpoint is not null)
        {
            return trackedCheckpoint;
        }

        DirectoryCheckpointRow? checkpoint = await context.DirectoryCheckpoints
            .FirstOrDefaultAsync(row => row.LibraryRootId == rootId.Value &&
                                        row.NormalizedRelativeDirectory == relativeDirectory,
                cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null)
        {
            return checkpoint;
        }

        checkpoint = new DirectoryCheckpointRow
        {
            LibraryRootId = rootId.Value,
            NormalizedRelativeDirectory = relativeDirectory,
            LastCompletedUtc = DateTimeOffset.MinValue,
            ScanState = 0,
        };
        context.DirectoryCheckpoints.Add(checkpoint);
        return checkpoint;
    }

    private async Task PersistDirectoryDiagnosticAsync(
        LibraryRootId rootId,
        long sessionId,
        DiscoveryDirectoryDiagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        CatalogueDbContext context = await _contextFactory!
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            DirectoryCheckpointRow directoryCheckpoint = await GetCheckpointAsync(
                    context,
                    rootId,
                    diagnostic.RelativeDirectory,
                    cancellationToken)
                .ConfigureAwait(false);
            directoryCheckpoint.LastStartedUtc ??= diagnostic.OccurredUtc;
            directoryCheckpoint.LastScanSessionId = sessionId;
            directoryCheckpoint.LastObservedFileCount = diagnostic.FilesSeen;
            directoryCheckpoint.LastErrorCode = diagnostic.ErrorCode;
            directoryCheckpoint.ScanState = diagnostic.Status == DiscoveryDirectoryStatus.Started
                ? 1
                : diagnostic.Status == DiscoveryDirectoryStatus.Completed ? 0 : 2;
            if (diagnostic.Status == DiscoveryDirectoryStatus.Completed)
            {
                directoryCheckpoint.LastCompletedUtc = diagnostic.OccurredUtc;
            }

            DirectoryCheckpointRow rootCheckpoint = await GetCheckpointAsync(
                    context,
                    rootId,
                    string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            rootCheckpoint.LastScanSessionId = sessionId;
            rootCheckpoint.ScanState = diagnostic.Status == DiscoveryDirectoryStatus.Failed ? 2 : 1;
            rootCheckpoint.LastErrorCode = diagnostic.ErrorCode;
            if (diagnostic.Status == DiscoveryDirectoryStatus.Completed)
            {
                rootCheckpoint.ResumeCursorRelativeDirectory = diagnostic.RelativeDirectory;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildSubjectKey(
        LibraryRootId rootId,
        string relativePath,
        long sizeBytes,
        long modifiedUtcTicks)
    {
        string identity = $"discovery-v1|{rootId.Value}|{relativePath}|{sizeBytes}|{modifiedUtcTicks}";
        return Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(identity)));
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;
        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
