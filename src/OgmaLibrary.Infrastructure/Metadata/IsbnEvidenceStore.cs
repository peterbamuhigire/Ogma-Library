using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>Durable store for ranked ISBN observations from extraction.</summary>
public sealed class IsbnEvidenceStore : IIsbnEvidenceStore
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Runtime constructor using an operation-scoped context.</summary>
    [ActivatorUtilitiesConstructor]
    public IsbnEvidenceStore(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>Test constructor using an existing context.</summary>
    internal IsbnEvidenceStore(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(
        string bookId,
        long extractionArtifactId,
        IReadOnlyList<IsbnCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extractionArtifactId);
        ArgumentNullException.ThrowIfNull(candidates);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<ExtractedIsbnEvidenceRow> existing = await context.ExtractedIsbnEvidence
            .Where(row => row.BookId == bookId && row.ExtractionArtifactId == extractionArtifactId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.ExtractedIsbnEvidence.RemoveRange(existing);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        int rank = 0;
        foreach (IsbnCandidate candidate in candidates)
        {
            if (!seen.Add(candidate.Isbn.Normalized))
            {
                continue;
            }

            context.ExtractedIsbnEvidence.Add(new ExtractedIsbnEvidenceRow
            {
                BookId = bookId,
                ExtractionArtifactId = extractionArtifactId,
                IsbnNormalized = candidate.Isbn.Normalized,
                IdentifierKind = candidate.Isbn.Normalized.Length == 10 ? 0 : 1,
                Source = (int)candidate.Source,
                Rank = rank,
                IsBest = rank == 0,
                DetectedUtc = now,
            });
            rank++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
