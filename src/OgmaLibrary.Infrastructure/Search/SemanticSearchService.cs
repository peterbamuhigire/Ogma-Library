using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Bounded exact semantic search over locally stored embedding vectors. The
/// candidate scan is streamed and retains only the requested top-K window, so
/// target-scale searches do not materialize the vector corpus in memory.
/// </summary>
public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int ActiveBookStatus = 0;
    private const int OversampleMultiplier = 4;
    private const int MaxCorpusVectors = 50_000;

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
        OllamaEmbeddingResult query = await _provider
            .EmbedAsync(queryText, EmbeddingGenerationService.DefaultModelName, cancellationToken)
            .ConfigureAwait(false);
        if (query.Vector.Length == 0 ||
            query.Vector.Length > 4096 ||
            query.Vector.Any(value => !float.IsFinite(value)))
        {
            return ExactFallback(
                exact,
                maxResults,
                providerUnavailable: false,
                availability: SemanticSearchAvailability.Degraded);
        }

        IReadOnlyList<ScoredVectorCandidate> corpus = await LoadTopCorpusAsync(
                query.Vector,
                Math.Max(maxResults * OversampleMultiplier, maxResults),
                cancellationToken)
            .ConfigureAwait(false);
        if (corpus.Count == 0)
        {
            return ExactFallback(
                exact,
                maxResults,
                providerUnavailable: false,
                availability: SemanticSearchAvailability.NoIndex);
        }

        List<SemanticSearchResult> semanticResults = corpus
            .GroupBy(item => item.Row.BookId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Row.ChunkId)
                .First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row.BookId, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(item => new SemanticSearchResult(
                item.Row.BookId,
                item.Row.Title,
                item.Row.ChunkId,
                item.Row.Source,
                CreateSnippet(item.Row.Text),
                item.Score,
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
            maxResults,
            HybridDiversityPolicy.Default);
        List<SemanticSearchResult> results = ranked
            .Select(ToSemanticSearchResult)
            .ToList();

        return new SemanticSearchResponse(
            ProviderUnavailable: false,
            UsedExactFallback: false,
            Results: results,
            Availability: results.Count == 0
                ? SemanticSearchAvailability.NoMatches
                : SemanticSearchAvailability.Ready);
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

        return ExactFallback(exact, maxResults, providerUnavailable, SemanticSearchAvailability.Degraded);
    }

    private SemanticSearchResponse ExactFallback(
        IReadOnlyList<CombinedSearchResult> exact,
        int maxResults,
        bool providerUnavailable,
        SemanticSearchAvailability availability)
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
                    ConfidenceLabel: null,
                    PageIndex: result.FtsHits.Count > 0 ? result.FtsHits[0].PageIndex : null,
                    PageJumpTarget: result.FtsHits.Count > 0 ? result.FtsHits[0].PageJumpTarget : null))
                .Take(maxResults)
                .ToList(),
            availability);
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
            ConfidenceLabel: enrichment.ConfidenceLabel,
            PageIndex: semantic?.PageIndex ?? fts?.PageIndex,
            PageJumpTarget: semantic?.PageJumpTarget ?? fts?.PageJumpTarget);
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

    private async Task<IReadOnlyList<ScoredVectorCandidate>> LoadTopCorpusAsync(
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryVector);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var rows = context.EmbeddingVectors
            .AsNoTracking()
            .Where(vector =>
                !vector.IsTombstoned &&
                vector.ModelName == EmbeddingGenerationService.DefaultModelName &&
                vector.ModelVersion == EmbeddingGenerationService.DefaultModelVersion &&
                vector.ProviderKey == EmbeddingGenerationService.DefaultProviderKey &&
                vector.DimensionCount == queryVector.Length)
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
            .Take(MaxCorpusVectors)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        var top = new PriorityQueue<VectorCandidateRow, VectorScorePriority>();
        await foreach (var row in rows.ConfigureAwait(false))
        {
            if (row.VectorBlob is null || row.VectorBlob.Length == 0)
            {
                continue;
            }

            VectorCandidateRow candidate = new(
                row.ChunkId,
                row.BookId,
                row.Title,
                (SearchChunkSource)row.Source,
                row.ChunkText ?? string.Empty,
                row.DimensionCount,
                Deserialize(row.VectorBlob, row.DimensionCount));
            if (candidate.Vector.Length != candidate.DimensionCount)
            {
                continue;
            }

            float score = CosineSimilarityService.Score(queryVector, candidate.Vector);
            var priority = new VectorScorePriority(score, candidate.ChunkId);
            if (top.Count < topK)
            {
                top.Enqueue(candidate, priority);
            }
            else if (top.TryPeek(out _, out VectorScorePriority worst) &&
                     priority.CompareTo(worst) > 0)
            {
                top.DequeueEnqueue(candidate, priority);
            }
        }

        return top.UnorderedItems
            .Select(item => new ScoredVectorCandidate(item.Element, item.Priority.Score))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row.ChunkId)
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
        int DimensionCount,
        float[] Vector);

    private sealed record ScoredVectorCandidate(
        VectorCandidateRow Row,
        float Score);

    private readonly record struct VectorScorePriority(float Score, long ChunkId) : IComparable<VectorScorePriority>
    {
        public int CompareTo(VectorScorePriority other)
        {
            int scoreComparison = Score.CompareTo(other.Score);
            return scoreComparison != 0
                ? scoreComparison
                : other.ChunkId.CompareTo(ChunkId);
        }
    }

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
