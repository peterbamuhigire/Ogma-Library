using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Brute-force semantic search over locally stored embedding vectors. ANN is
/// intentionally deferred by Phase 11 until the spike/ADR gate.
/// </summary>
public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int ActiveBookStatus = 0;
    private const int OversampleMultiplier = 4;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IOllamaEmbeddingProvider _provider;
    private readonly ICombinedSearchService _exactSearch;
    private readonly IHybridRankingService _hybridRanking;
    private readonly IMatchLocationService _matchLocations;

    /// <summary>
    /// Initializes a new instance of <see cref="SemanticSearchService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public SemanticSearchService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IOllamaEmbeddingProvider provider,
        ICombinedSearchService exactSearch,
        IHybridRankingService hybridRanking,
        IMatchLocationService matchLocations)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(exactSearch);
        ArgumentNullException.ThrowIfNull(hybridRanking);
        ArgumentNullException.ThrowIfNull(matchLocations);

        _contextFactory = contextFactory;
        _provider = provider;
        _exactSearch = exactSearch;
        _hybridRanking = hybridRanking;
        _matchLocations = matchLocations;
    }

    internal SemanticSearchService(
        CatalogueDbContext context,
        IOllamaEmbeddingProvider provider,
        ICombinedSearchService exactSearch,
        IHybridRankingService? hybridRanking = null,
        IMatchLocationService? matchLocations = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(exactSearch);

        _context = context;
        _provider = provider;
        _exactSearch = exactSearch;
        _hybridRanking = hybridRanking ?? new HybridRankingService();
        _matchLocations = matchLocations ?? new MatchLocationService();
    }

    /// <inheritdoc />
    public async Task<SemanticSearchResponse> SearchAsync(
        string queryText,
        int maxResults,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        if (!await _provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return await ExactFallbackAsync(queryText, maxResults, providerUnavailable: true, cancellationToken)
                .ConfigureAwait(false);
        }

        IReadOnlyList<CombinedSearchResult> exact = await _exactSearch
            .SearchAsync(queryText, maxResults * OversampleMultiplier, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<VectorCandidateRow> corpus = await LoadCorpusAsync(cancellationToken)
            .ConfigureAwait(false);
        if (corpus.Count == 0)
        {
            return ExactFallback(exact, maxResults, providerUnavailable: false);
        }

        OllamaEmbeddingResult query = await _provider
            .EmbedAsync(queryText, EmbeddingGenerationService.DefaultModelName, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<VectorSearchHit> hits = CosineSimilarityService.TopK(
            query.Vector,
            corpus.Select(row => new VectorSearchCandidate(row.ChunkId, row.Vector)),
            Math.Max(maxResults * OversampleMultiplier, maxResults));
        Dictionary<long, VectorCandidateRow> byChunk = corpus.ToDictionary(row => row.ChunkId);

        List<SemanticSearchResult> semanticResults = hits
            .Select(hit => (Hit: hit, Row: byChunk[hit.ChunkId]))
            .GroupBy(item => item.Row.BookId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Hit.Score)
                .ThenBy(item => item.Row.ChunkId)
                .First())
            .OrderByDescending(item => item.Hit.Score)
            .ThenBy(item => item.Row.BookId, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(item => new SemanticSearchResult(
                item.Row.BookId,
                item.Row.Title,
                item.Row.ChunkId,
                item.Row.Source,
                CreateSnippet(item.Row.Text),
                item.Hit.Score,
                ExactFallback: false))
            .ToList();

        IReadOnlyDictionary<string, HybridBookSignals> signals = await LoadBookSignalsAsync(
                exact.Select(result => result.BookId)
                    .Concat(semanticResults.Select(result => result.BookId))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<HybridRankedResult> ranked = _hybridRanking.Rank(
            exact,
            semanticResults,
            signals,
            HybridRankingWeights.Default,
            DateTimeOffset.UtcNow,
            maxResults);
        List<SemanticSearchResult> results = ranked
            .Select(ToSemanticSearchResult)
            .ToList();

        return new SemanticSearchResponse(
            ProviderUnavailable: false,
            UsedExactFallback: false,
            Results: results);
    }

    private async Task<SemanticSearchResponse> ExactFallbackAsync(
        string queryText,
        int maxResults,
        bool providerUnavailable,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CombinedSearchResult> exact = await _exactSearch
            .SearchAsync(queryText, maxResults, cancellationToken)
            .ConfigureAwait(false);

        return ExactFallback(exact, maxResults, providerUnavailable);
    }

    private SemanticSearchResponse ExactFallback(
        IReadOnlyList<CombinedSearchResult> exact,
        int maxResults,
        bool providerUnavailable)
    {
        return new SemanticSearchResponse(
            providerUnavailable,
            UsedExactFallback: true,
            exact.Select(result => new SemanticSearchResult(
                    result.BookId,
                    result.Title,
                    result.FtsHits.Count > 0 ? result.FtsHits[0].ChunkId : null,
                    result.FtsHits.Count > 0 ? result.FtsHits[0].Source : null,
                    result.FtsHits.Count > 0 ? result.FtsHits[0].Snippet : null,
                    SemanticScore: null,
                    ExactFallback: true,
                    HybridScore: null,
                    MatchLocations: _matchLocations.GetLocations(result, semanticResult: null),
                    ConfidenceLabel: null))
                .Take(maxResults)
                .ToList());
    }

    private SemanticSearchResult ToSemanticSearchResult(HybridRankedResult result)
    {
        SearchResultEnrichment enrichment = _matchLocations.Enrich(result);
        SemanticSearchResult? semantic = result.SemanticResult;
        CombinedSearchResult? exact = result.ExactResult;
        FtsSearchResult? fts = exact?.FtsHits.Count > 0 ? exact.FtsHits[0] : null;

        return new SemanticSearchResult(
            result.BookId,
            result.Title,
            semantic?.ChunkId ?? fts?.ChunkId,
            semantic?.Source ?? fts?.Source,
            semantic?.Snippet ?? fts?.Snippet,
            semantic?.SemanticScore,
            ExactFallback: false,
            HybridScore: result.HybridScore,
            MatchLocations: enrichment.MatchLocations,
            ConfidenceLabel: enrichment.ConfidenceLabel);
    }

    private async Task<IReadOnlyDictionary<string, HybridBookSignals>> LoadBookSignalsAsync(
        string[] bookIds,
        CancellationToken cancellationToken)
    {
        if (bookIds.Length == 0)
        {
            return new Dictionary<string, HybridBookSignals>(StringComparer.Ordinal);
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<BookSignalRow> rows = await context.Books
            .AsNoTracking()
            .Where(book => bookIds.Contains(book.BookId))
            .GroupJoin(
                context.ReadingProgress.AsNoTracking(),
                book => book.BookId,
                progress => progress.BookId,
                (book, progress) => new { book, progress })
            .SelectMany(
                item => item.progress.DefaultIfEmpty(),
                (item, progress) => new BookSignalRow(
                    item.book.BookId,
                    progress == null ? null : progress.LastReadUtc,
                    progress == null ? null : (ReadingStatus?)((ReadingStatus)progress.Status),
                    item.book.Rating))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.BookId,
            row => new HybridBookSignals(
                row.BookId,
                row.LastReadUtc,
                row.ReadingStatus,
                row.Rating),
            StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<VectorCandidateRow>> LoadCorpusAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var rows = await context.EmbeddingVectors
            .AsNoTracking()
            .Where(vector =>
                vector.ModelName == EmbeddingGenerationService.DefaultModelName &&
                vector.ModelVersion == EmbeddingGenerationService.DefaultModelVersion)
            .Join(
                context.SearchChunks.AsNoTracking(),
                vector => vector.ChunkId,
                chunk => chunk.ChunkId,
                (vector, chunk) => new { vector, chunk })
            .Join(
                context.Books.AsNoTracking().Where(book => book.Status == ActiveBookStatus),
                item => item.chunk.BookId,
                book => book.BookId,
                (item, book) => new
                {
                    item.vector.ChunkId,
                    item.vector.VectorBlob,
                    item.vector.DimensionCount,
                    item.chunk.BookId,
                    item.chunk.ChunkText,
                    item.chunk.Source,
                    book.Title,
                })
            .OrderBy(row => row.ChunkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(row => row.VectorBlob is not null && row.VectorBlob.Length > 0)
            .Select(row => new VectorCandidateRow(
                row.ChunkId,
                row.BookId,
                row.Title,
                (SearchChunkSource)row.Source,
                row.ChunkText ?? string.Empty,
                Deserialize(row.VectorBlob!, row.DimensionCount)))
            .Where(row => row.Vector.Length > 0)
            .ToList();
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private static float[] Deserialize(byte[] bytes, int dimensionCount)
    {
        int count = Math.Min(dimensionCount, bytes.Length / sizeof(float));
        var vector = new float[count];
        Buffer.BlockCopy(bytes, 0, vector, 0, count * sizeof(float));
        return vector;
    }

    private static string CreateSnippet(string text)
    {
        string normalized = string.Join(' ', text.Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private sealed record VectorCandidateRow(
        long ChunkId,
        string BookId,
        string? Title,
        SearchChunkSource Source,
        string Text,
        float[] Vector);

    private sealed record BookSignalRow(
        string BookId,
        DateTimeOffset? LastReadUtc,
        ReadingStatus? ReadingStatus,
        int? Rating);

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
