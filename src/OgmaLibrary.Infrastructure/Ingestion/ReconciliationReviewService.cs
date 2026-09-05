using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Applies only explicit, candidate-bound relocation decisions.</summary>
public sealed class ReconciliationReviewService : IReconciliationReviewService
{
    private const int MaximumPendingReviews = 500;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal ReconciliationReviewService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public ReconciliationReviewService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReconciliationReviewDescriptor>> ListPendingAsync(
        string? libraryRootId = null,
        CancellationToken cancellationToken = default)
    {
        if (libraryRootId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(libraryRootId);
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        IQueryable<ReconciliationReviewRow> query = lease.Context.ReconciliationReviews
            .AsNoTracking()
            .Where(row => row.Status == (int)ReconciliationReviewDecision.Accept - 1);
        if (libraryRootId is not null)
        {
            query = query.Where(row => row.LibraryRootId == libraryRootId);
        }

        // SQLite cannot translate DateTimeOffset ordering. Load the already
        // bounded pending set, then apply the stable presentation order in
        // managed code so the same contract works across providers.
        List<ReconciliationReviewRow> rows = await query
            .OrderBy(row => row.ReconciliationReviewId)
            .Take(MaximumPendingReviews)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.CreatedUtc)
            .ThenBy(row => row.ReconciliationReviewId)
            .Select(Map)
            .ToList();
    }

    /// <inheritdoc />
    public async Task DecideAsync(
        long reviewId,
        ReconciliationReviewDecision decision,
        string? selectedRelativePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ReconciliationReviewRow review = await context.ReconciliationReviews
            .FirstOrDefaultAsync(row => row.ReconciliationReviewId == reviewId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Reconciliation review '{reviewId}' was not found.");
        if (review.Status != 0)
        {
            throw new InvalidOperationException("Only a pending reconciliation review can be decided.");
        }

        DateTimeOffset decidedUtc = DateTimeOffset.UtcNow;
        if (decision == ReconciliationReviewDecision.Accept)
        {
            string candidate = NormalizeCandidate(selectedRelativePath);
            string[] candidates = ReadCandidates(review.CandidatePathsJson);
            if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The selected path is not one of the candidates retained for this review.",
                    nameof(selectedRelativePath));
            }

            FileOccurrenceRow occurrence = await context.FileOccurrences
                .FirstOrDefaultAsync(row => row.FileOccurrenceId == review.FileOccurrenceId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException(
                    $"File occurrence '{review.FileOccurrenceId}' was not found.");
            if (occurrence.LibraryRootId != review.LibraryRootId)
            {
                throw new InvalidOperationException("The review and occurrence belong to different roots.");
            }

            bool pathTaken = await context.FileOccurrences.AnyAsync(
                row => row.LibraryRootId == review.LibraryRootId &&
                       row.FileOccurrenceId != review.FileOccurrenceId &&
                       row.NormalizedRelativePath == candidate,
                cancellationToken).ConfigureAwait(false);
            if (pathTaken)
            {
                throw new InvalidOperationException("The selected path is already assigned to another occurrence.");
            }

            occurrence.RelativePath = candidate;
            occurrence.NormalizedRelativePath = candidate;
            occurrence.AvailabilityStatus = (int)AvailabilityStatus.Available;
            occurrence.LastSeenUtc = decidedUtc;
            occurrence.MissingSinceUtc = null;
            review.Status = (int)ReconciliationReviewDecision.Accept;
            AddAudit(context, occurrence.FileOccurrenceId, "relocation_review_accepted");
        }
        else
        {
            review.Status = (int)ReconciliationReviewDecision.Reject;
            AddAudit(context, review.FileOccurrenceId, "relocation_review_rejected");
        }

        review.DecidedUtc = decidedUtc;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ReconciliationReviewDescriptor Map(ReconciliationReviewRow row) =>
        new(
            row.ReconciliationReviewId,
            row.LibraryRootId,
            row.FileOccurrenceId,
            row.ReasonCode,
            ReadCandidates(row.CandidatePathsJson),
            row.CreatedUtc);

    private static string[] ReadCandidates(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ?? [])
                .Select(NormalizeCandidate)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return [];
        }
    }

    private static string NormalizeCandidate(string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || Path.IsPathRooted(path) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The relocation path must be a safe root-relative path.", nameof(path));
        }

        return normalized;
    }

    private static void AddAudit(CatalogueDbContext context, string occurrenceId, string reason) =>
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "FilesystemReconciliation",
            EntityId = occurrenceId,
            EntityType = "FileOccurrence",
            AfterJson = $"{{\"reason\":\"{reason}\"}}",
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });

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
