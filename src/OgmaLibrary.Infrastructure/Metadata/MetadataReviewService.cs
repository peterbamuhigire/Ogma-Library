using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>Stores proposal cards and applies only explicit review decisions.</summary>
public sealed class MetadataReviewService : IMetadataReviewService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IMetadataApplyService _applyService;

    /// <summary>Test constructor using an existing context.</summary>
    internal MetadataReviewService(CatalogueDbContext context, IMetadataApplyService applyService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _applyService = applyService ?? throw new ArgumentNullException(nameof(applyService));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public MetadataReviewService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataApplyService applyService,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _applyService = applyService ?? throw new ArgumentNullException(nameof(applyService));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MetadataProposalDescriptor>> CreateAsync(
        string bookId,
        IReadOnlyList<MergedMetadataProposal> proposals,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(proposals);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        bool bookExists = await context.Books.AnyAsync(
            book => book.BookId == bookId, cancellationToken).ConfigureAwait(false);
        if (!bookExists)
        {
            throw new KeyNotFoundException($"Book '{bookId}' was not found.");
        }

        var rows = new List<MetadataProposalRow>();
        foreach (MergedMetadataProposal proposal in proposals)
        {
            Validate(proposal);
            string alternatives = JsonSerializer.Serialize(proposal.Alternatives.Take(16));
            if (alternatives.Length > 65536)
            {
                throw new ArgumentException("Metadata alternatives exceed the storage limit.");
            }

            rows.Add(new MetadataProposalRow
            {
                BookId = bookId,
                FieldName = proposal.FieldName,
                ProposedValue = proposal.ProposedValue,
                CurrentValue = proposal.CurrentValue,
                Confidence = proposal.MergedConfidence,
                Source = proposal.WinningProvider,
                AlternativesJson = alternatives,
                Status = (int)MetadataProposalStatus.Pending,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }

        context.MetadataProposals.AddRange(rows);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MetadataProposalDescriptor>> ListPendingAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<MetadataProposalRow> rows = await lease.Context.MetadataProposals
            .AsNoTracking()
            .Where(row => row.BookId == bookId && row.Status == (int)MetadataProposalStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.OrderBy(row => row.CreatedUtc).ThenBy(row => row.MetadataProposalId).Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<MetadataProposalDescriptor> DecideAsync(
        long proposalId,
        bool accept,
        string? editedValue = null,
        bool userOverride = false,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        MetadataProposalRow proposal = await context.MetadataProposals
            .FirstOrDefaultAsync(row => row.MetadataProposalId == proposalId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Metadata proposal '{proposalId}' was not found.");
        if (proposal.Status != (int)MetadataProposalStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending proposal can be decided.");
        }

        if (accept)
        {
            string source = userOverride ? "UserOverride" : proposal.Source;
            double confidence = userOverride ? 1.0 : proposal.Confidence;
            await _applyService.ApplyMergedMetadataAsync(
                proposal.BookId,
                [new AcceptedFieldProposal(
                    proposal.FieldName,
                    editedValue ?? proposal.ProposedValue,
                    source,
                    confidence,
                    userOverride)],
                cancellationToken).ConfigureAwait(false);
            proposal.Status = (int)MetadataProposalStatus.Accepted;
        }
        else
        {
            proposal.Status = (int)MetadataProposalStatus.Rejected;
        }

        proposal.DecidedUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(proposal);
    }

    private static void Validate(MergedMetadataProposal proposal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.FieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.WinningProvider);
        if (proposal.FieldName.Length > 128 || proposal.WinningProvider.Length > 128 ||
            proposal.ProposedValue?.Length > 4096 || proposal.CurrentValue?.Length > 4096)
        {
            throw new ArgumentException("Metadata proposal fields exceed their storage limits.");
        }

        if (proposal.MergedConfidence is < 0.0 or > 1.0 || double.IsNaN(proposal.MergedConfidence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(proposal), "Metadata proposal confidence must be within [0.0, 1.0].");
        }
    }

    private static MetadataProposalDescriptor Map(MetadataProposalRow row) => new(
        row.MetadataProposalId,
        row.BookId,
        row.FieldName,
        row.ProposedValue,
        row.CurrentValue,
        row.Confidence,
        row.Source,
        DeserializeAlternatives(row.AlternativesJson),
        (MetadataProposalStatus)row.Status,
        row.CreatedUtc,
        row.DecidedUtc);

    private static List<AlternativeFieldValue> DeserializeAlternatives(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<AlternativeFieldValue>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
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
