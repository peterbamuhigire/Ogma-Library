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
        Task discoveryTask = _discovery.DiscoverAsync(
            root.CanonicalLocator,
            excludedFolders ?? [],
            channel.Writer,
            cancellationToken);

        int seen = 0;
        int changed = 0;
        int unchanged = 0;
        int failed = 0;
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        DirectoryCheckpointRow checkpoint = await GetCheckpointAsync(context, rootId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedUtc = DateTimeOffset.UtcNow;

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
                observation.LastSeenUtc = DateTimeOffset.UtcNow;
                if (isChanged)
                {
                    changed++;
                    await _processing.EnqueueStageAsync(
                        session.Id,
                        "FileProcessing",
                        $"{rootId.Value}:{normalizedPath}",
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
        completedUtc = DateTimeOffset.UtcNow;
        checkpoint.LastCompletedUtc = completedUtc;
        checkpoint.LastObservedFileCount = seen;
        checkpoint.LastErrorCode = failed == 0 ? null : "observation_persist_failed";
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _roots.RecordSuccessfulScanAsync(rootId, cancellationToken).ConfigureAwait(false);
        return new DiscoveryScanResult(session, seen, changed, unchanged, failed, completedUtc);
    }

    private static async Task<DirectoryCheckpointRow> GetCheckpointAsync(
        CatalogueDbContext context,
        LibraryRootId rootId,
        CancellationToken cancellationToken)
    {
        DirectoryCheckpointRow? checkpoint = await context.DirectoryCheckpoints
            .FirstOrDefaultAsync(row => row.LibraryRootId == rootId.Value &&
                                        row.NormalizedRelativeDirectory == string.Empty,
                cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null)
        {
            return checkpoint;
        }

        checkpoint = new DirectoryCheckpointRow
        {
            LibraryRootId = rootId.Value,
            NormalizedRelativeDirectory = string.Empty,
            LastCompletedUtc = DateTimeOffset.MinValue,
        };
        context.DirectoryCheckpoints.Add(checkpoint);
        return checkpoint;
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
