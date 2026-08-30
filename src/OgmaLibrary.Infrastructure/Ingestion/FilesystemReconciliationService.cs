using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Applies root-scoped presence evidence without inferring deletion on outage.</summary>
public sealed class FilesystemReconciliationService : IFilesystemReconciliationService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal FilesystemReconciliationService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public FilesystemReconciliationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<ReconciliationResult> ReconcileAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ScanSessionRow session = await context.ScanSessions
            .FirstOrDefaultAsync(row => row.ScanSessionId == scanSessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Scan session '{scanSessionId}' was not found.");
        LibraryRootRow root = await context.LibraryRoots
            .FirstOrDefaultAsync(row => row.LibraryRootId == session.LibraryRootId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Library root '{session.LibraryRootId}' was not found.");

        DateTimeOffset evaluatedUtc = DateTimeOffset.UtcNow;
        LibraryRootId rootId = new(session.LibraryRootId);
        DirectoryCheckpointRow? checkpoint = await context.DirectoryCheckpoints
            .FirstOrDefaultAsync(row => row.LibraryRootId == session.LibraryRootId &&
                                        row.NormalizedRelativeDirectory == string.Empty,
                cancellationToken)
            .ConfigureAwait(false);
        if (root.RootStatus != (int)LibraryRootStatus.Available ||
            string.IsNullOrWhiteSpace(root.CanonicalLocator))
        {
            return new ReconciliationResult(
                scanSessionId, rootId, ReconciliationOutcome.RootUnavailable, 0, 0, 0, 0, evaluatedUtc);
        }

        if (checkpoint is null || checkpoint.LastCompletedUtc < session.StartedUtc ||
            checkpoint.LastErrorCode is not null)
        {
            return new ReconciliationResult(
                scanSessionId, rootId, ReconciliationOutcome.IncompleteScan, 0, 0, 0, 0, evaluatedUtc);
        }

        HashSet<string> observed = (await context.DiscoveryObservations
            .Where(row => row.LibraryRootId == session.LibraryRootId &&
                          row.LastObservedScanSessionId == scanSessionId)
            .Select(row => row.NormalizedRelativePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<DiscoveryObservationRow> observedRows = await context.DiscoveryObservations
            .Where(row => row.LibraryRootId == session.LibraryRootId &&
                          row.LastObservedScanSessionId == scanSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, List<DiscoveryObservationRow>> observationsByHash = observedRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Sha256Hash))
            .GroupBy(row => row.Sha256Hash!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DiscoveryObservationRow> observationsByPath = observedRows
            .ToDictionary(row => row.NormalizedRelativePath, StringComparer.OrdinalIgnoreCase);
        List<FileOccurrenceRow> occurrences = await context.FileOccurrences
            .Where(row => row.LibraryRootId == session.LibraryRootId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        string[] assetIds = occurrences
            .Where(occurrence => occurrence.ContentAssetId is not null)
            .Select(occurrence => occurrence.ContentAssetId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, string> assetHashes = await context.ContentAssets
            .Where(asset => assetIds.Contains(asset.ContentAssetId))
            .ToDictionaryAsync(asset => asset.ContentAssetId, asset => asset.Sha256Hash, cancellationToken)
            .ConfigureAwait(false);
        int restored = 0;
        int unavailable = 0;
        int moved = 0;
        int replacements = 0;
        foreach (FileOccurrenceRow occurrence in occurrences)
        {
            string normalized = Normalize(occurrence.RelativePath);
            bool present = observed.Contains(normalized);
            if (!present && occurrence.ContentAssetId is not null)
            {
                if (assetHashes.TryGetValue(occurrence.ContentAssetId, out string? assetHash) &&
                    observationsByHash.TryGetValue(assetHash, out List<DiscoveryObservationRow>? matches) &&
                    matches.Count == 1)
                {
                    DiscoveryObservationRow match = matches[0];
                    occurrence.RelativePath = match.NormalizedRelativePath;
                    occurrence.NormalizedRelativePath = match.NormalizedRelativePath;
                    occurrence.SizeBytes = match.SizeBytes;
                    occurrence.ModifiedUtcTicks = match.ModifiedUtcTicks;
                    occurrence.LastSeenUtc = evaluatedUtc;
                    occurrence.AvailabilityStatus = (int)AvailabilityStatus.Available;
                    moved++;
                    AddAudit(context, occurrence.FileOccurrenceId, "moved_by_exact_hash");
                    continue;
                }
            }
            if (present && occurrence.AvailabilityStatus != (int)AvailabilityStatus.Available)
            {
                occurrence.AvailabilityStatus = (int)AvailabilityStatus.Available;
                occurrence.LastSeenUtc = evaluatedUtc;
                restored++;
                AddAudit(context, occurrence.FileOccurrenceId, "restored");
            }
            else if (!present && occurrence.AvailabilityStatus == (int)AvailabilityStatus.Available)
            {
                occurrence.AvailabilityStatus = (int)AvailabilityStatus.Unavailable;
                unavailable++;
                AddAudit(context, occurrence.FileOccurrenceId, "not_observed");
            }
            else if (present)
            {
                occurrence.LastSeenUtc = evaluatedUtc;
                observationsByPath.TryGetValue(normalized, out DiscoveryObservationRow? observation);
                if (observation?.Sha256Hash is not null && occurrence.ContentAssetId is not null)
                {
                    if (assetHashes.TryGetValue(occurrence.ContentAssetId, out string? assetHash) &&
                        !string.Equals(assetHash, observation.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        occurrence.ContentAssetId = null;
                        occurrence.SizeBytes = observation.SizeBytes;
                        occurrence.ModifiedUtcTicks = observation.ModifiedUtcTicks;
                        replacements++;
                        AddAudit(context, occurrence.FileOccurrenceId, "replacement_requires_reprocessing");
                    }
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReconciliationResult(
            scanSessionId,
            rootId,
            ReconciliationOutcome.Applied,
            restored,
            unavailable,
            moved,
            replacements,
            evaluatedUtc);
    }

    private static void AddAudit(CatalogueDbContext context, string occurrenceId, string reason)
    {
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "FilesystemReconciliation",
            EntityId = occurrenceId,
            EntityType = "FileOccurrence",
            AfterJson = $"{{\"reason\":\"{reason}\"}}",
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

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
