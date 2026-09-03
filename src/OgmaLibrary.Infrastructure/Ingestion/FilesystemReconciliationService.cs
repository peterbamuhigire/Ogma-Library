using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Applies root-scoped presence evidence without inferring deletion on outage.</summary>
public sealed class FilesystemReconciliationService : IFilesystemReconciliationService
{
    private static readonly TimeSpan DefaultMissingGracePeriod = TimeSpan.FromHours(24);
    private static readonly string[] InvalidatedStages = ["FileProcessing"];
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly TimeSpan _missingGracePeriod;

    /// <summary>Test constructor using an existing context.</summary>
    internal FilesystemReconciliationService(CatalogueDbContext context)
        : this(context, DefaultMissingGracePeriod)
    {
    }

    /// <summary>Test constructor allowing deterministic grace-window coverage.</summary>
    internal FilesystemReconciliationService(CatalogueDbContext context, TimeSpan missingGracePeriod)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _missingGracePeriod = ValidateGracePeriod(missingGracePeriod);
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public FilesystemReconciliationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
        : this(contextFactory, serviceProvider, DefaultMissingGracePeriod)
    {
    }

    /// <summary>DI constructor with an explicit missing-file grace period.</summary>
    public FilesystemReconciliationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider,
        TimeSpan missingGracePeriod)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _missingGracePeriod = ValidateGracePeriod(missingGracePeriod);
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
        int deferred = 0;
        int ambiguous = 0;
        int invalidatedStages = 0;
        var auditSummary = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FileOccurrenceRow occurrence in occurrences)
        {
            string normalized = Normalize(occurrence.RelativePath);
            bool present = observed.Contains(normalized);
            if (!present && occurrence.ContentAssetId is not null)
            {
                if (assetHashes.TryGetValue(occurrence.ContentAssetId, out string? assetHash) &&
                    observationsByHash.TryGetValue(assetHash, out List<DiscoveryObservationRow>? matches))
                {
                    if (matches.Count == 1)
                    {
                        DiscoveryObservationRow match = matches[0];
                        occurrence.RelativePath = match.NormalizedRelativePath;
                        occurrence.NormalizedRelativePath = match.NormalizedRelativePath;
                        occurrence.SizeBytes = match.SizeBytes;
                        occurrence.ModifiedUtcTicks = match.ModifiedUtcTicks;
                        occurrence.LastSeenUtc = evaluatedUtc;
                        occurrence.MissingSinceUtc = null;
                        occurrence.AvailabilityStatus = (int)AvailabilityStatus.Available;
                        moved++;
                        AddAudit(context, occurrence.FileOccurrenceId, "moved_by_exact_hash", auditSummary);
                        continue;
                    }

                    if (matches.Count > 1)
                    {
                        occurrence.MissingSinceUtc ??= evaluatedUtc;
                        QueueRelocationReview(
                            context,
                            session.LibraryRootId,
                            occurrence.FileOccurrenceId,
                            matches,
                            auditSummary);
                        ambiguous++;
                        AddAudit(context, occurrence.FileOccurrenceId, "ambiguous_relocation_review", auditSummary);
                        continue;
                    }
                }
            }
            if (present && occurrence.AvailabilityStatus != (int)AvailabilityStatus.Available)
            {
                occurrence.AvailabilityStatus = (int)AvailabilityStatus.Available;
                occurrence.LastSeenUtc = evaluatedUtc;
                occurrence.MissingSinceUtc = null;
                restored++;
                AddAudit(context, occurrence.FileOccurrenceId, "restored", auditSummary);
            }
            else if (!present && occurrence.AvailabilityStatus == (int)AvailabilityStatus.Available)
            {
                occurrence.MissingSinceUtc ??= evaluatedUtc;
                if (evaluatedUtc - occurrence.MissingSinceUtc >= _missingGracePeriod)
                {
                    occurrence.AvailabilityStatus = (int)AvailabilityStatus.Unavailable;
                    unavailable++;
                    AddAudit(context, occurrence.FileOccurrenceId, "not_observed_after_grace", auditSummary);
                }
                else
                {
                    deferred++;
                    AddAudit(context, occurrence.FileOccurrenceId, "not_observed_grace", auditSummary);
                }
            }
            else if (present)
            {
                occurrence.LastSeenUtc = evaluatedUtc;
                occurrence.MissingSinceUtc = null;
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
                        invalidatedStages += InvalidateDownstreamStages(
                            context,
                            session,
                            occurrence,
                            observation.Sha256Hash,
                            auditSummary);
                        AddAudit(context, occurrence.FileOccurrenceId, "replacement_requires_reprocessing", auditSummary);
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
            evaluatedUtc,
            deferred,
            ambiguous,
            invalidatedStages,
            auditSummary.Select(pair => new ReconciliationAuditSummary(pair.Key, pair.Value)).ToArray());
    }

    private static int InvalidateDownstreamStages(
        CatalogueDbContext context,
        ScanSessionRow session,
        FileOccurrenceRow occurrence,
        string currentHash,
        Dictionary<string, int> auditSummary)
    {
        string subjectKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"reconciliation-v1|{session.LibraryRootId}|{occurrence.FileOccurrenceId}|{currentHash}")));
        int added = 0;
        foreach (string stageName in InvalidatedStages)
        {
            bool alreadyQueued = context.StageExecutions.Local.Any(stage =>
                stage.ScanSessionId == session.ScanSessionId &&
                stage.StageName == stageName &&
                stage.SubjectKey == subjectKey) || context.StageExecutions.Any(stage =>
                stage.ScanSessionId == session.ScanSessionId &&
                stage.StageName == stageName &&
                stage.SubjectKey == subjectKey);
            if (alreadyQueued)
            {
                continue;
            }

            context.StageExecutions.Add(new StageExecutionRow
            {
                ScanSessionId = session.ScanSessionId,
                StageName = stageName,
                SubjectKey = subjectKey,
                Status = (int)StageExecutionStatus.Pending,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            auditSummary["downstream_reprocessing_queued"] = auditSummary.GetValueOrDefault("downstream_reprocessing_queued") + added;
        }

        return added;
    }

    private static void QueueRelocationReview(
        CatalogueDbContext context,
        string rootId,
        string occurrenceId,
        IReadOnlyList<DiscoveryObservationRow> matches,
        Dictionary<string, int> auditSummary)
    {
        bool exists = context.ReconciliationReviews.Local.Any(review =>
            review.LibraryRootId == rootId &&
            review.FileOccurrenceId == occurrenceId &&
            review.Status == 0) || context.ReconciliationReviews.Any(review =>
            review.LibraryRootId == rootId &&
            review.FileOccurrenceId == occurrenceId &&
            review.Status == 0);
        if (exists)
        {
            return;
        }

        context.ReconciliationReviews.Add(new ReconciliationReviewRow
        {
            LibraryRootId = rootId,
            FileOccurrenceId = occurrenceId,
            ReasonCode = "ambiguous_relocation_review",
            CandidatePathsJson = JsonSerializer.Serialize(
                matches.Select(match => match.NormalizedRelativePath)
                    .OrderBy(path => path, StringComparer.Ordinal)),
            Status = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        auditSummary["relocation_review_queued"] = auditSummary.GetValueOrDefault("relocation_review_queued") + 1;
    }

    private static void AddAudit(
        CatalogueDbContext context,
        string occurrenceId,
        string reason,
        Dictionary<string, int> auditSummary)
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
        auditSummary[reason] = auditSummary.GetValueOrDefault(reason) + 1;
    }

    private static TimeSpan ValidateGracePeriod(TimeSpan value) =>
        value >= TimeSpan.Zero && value <= TimeSpan.FromDays(30)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "The missing-file grace period must be between 0 and 30 days.");

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
