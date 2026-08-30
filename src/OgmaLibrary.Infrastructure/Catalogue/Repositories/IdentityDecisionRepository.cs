using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>Stores decisions produced by the conservative domain identity policy.</summary>
public sealed class IdentityDecisionRepository : IIdentityDecisionService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal IdentityDecisionRepository(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public IdentityDecisionRepository(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<IdentityDecision> EvaluateAndRecordAsync(
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(candidate);
        IdentityDecision decision = IdentityDecisionPolicy.Evaluate(
            new IdentityDecisionId(CanonicalIdGenerator.NewId()), subject, candidate);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        IdentityDecisionRow? existing = await context.IdentityDecisions
            .FirstOrDefaultAsync(row =>
                row.SubjectOccurrenceId == decision.SubjectOccurrenceId.Value &&
                row.CandidateOccurrenceId == decision.CandidateOccurrenceId.Value &&
                row.PolicyVersion == decision.PolicyVersion,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Map(existing);
        }

        var row = new IdentityDecisionRow
        {
            IdentityDecisionId = decision.Id.Value,
            SubjectOccurrenceId = decision.SubjectOccurrenceId.Value,
            CandidateOccurrenceId = decision.CandidateOccurrenceId.Value,
            Relationship = (int)decision.Relationship,
            Disposition = (int)decision.Disposition,
            EvidenceTier = (int)decision.Tier,
            Confidence = decision.Confidence.Value,
            PolicyVersion = decision.PolicyVersion,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        context.IdentityDecisions.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return decision;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityDecision>> ListReviewRequiredAsync(
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<IdentityDecisionRow> rows = await lease.Context.IdentityDecisions
            .AsNoTracking()
            .Where(row => row.Disposition == (int)IdentityDecisionDisposition.ReviewRequired)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.CreatedUtc)
            .ThenBy(row => row.IdentityDecisionId)
            .Select(Map)
            .ToList();
    }

    private static IdentityDecision Map(IdentityDecisionRow row) => new(
        new IdentityDecisionId(row.IdentityDecisionId),
        new FileOccurrenceId(row.SubjectOccurrenceId),
        new FileOccurrenceId(row.CandidateOccurrenceId),
        (IdentityRelationship)row.Relationship,
        (IdentityDecisionDisposition)row.Disposition,
        (IdentityDecisionTier)row.EvidenceTier,
        new ConfidenceScore(row.Confidence),
        row.PolicyVersion);

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
