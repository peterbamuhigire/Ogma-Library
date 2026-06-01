using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP3 semantic search service tests.
/// </summary>
public sealed class SemanticSearchServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public SemanticSearchServiceTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task SemanticSearch_FindsRelevantBook_WithoutKeywordOverlap()
    {
        long relevant = SeedBookChunk(
            "P11SEMANTIC00000000001",
            "A Plain Title",
            "colonial school reform and public administration",
            rating: 5);
        long irrelevant = SeedBookChunk(
            "P11SEMANTIC00000000002",
            "Cooking Notes",
            "banana bread and kitchen practice");
        var repository = new EmbeddingVectorRepository(_context);
        await repository.CreateAsync(NewVector(relevant, [0.99f, 0.01f]), CancellationToken.None);
        await repository.CreateAsync(NewVector(irrelevant, [0.0f, 1.0f]), CancellationToken.None);
        var provider = new StubOllamaProvider
        {
            QueryVector = [1.0f, 0.0f],
        };
        var service = new SemanticSearchService(_context, provider, new StubExactSearch());

        SemanticSearchResponse response = await service.SearchAsync(
            "governance education Africa",
            5,
            CancellationToken.None);

        Assert.False(response.ProviderUnavailable);
        Assert.False(response.UsedExactFallback);
        SemanticSearchResult first = Assert.Single(response.Results.Take(1));
        Assert.Equal("P11SEMANTIC00000000001", first.BookId);
        Assert.False(first.ExactFallback);
        Assert.NotNull(first.SemanticScore);
        Assert.Equal(ConfidenceLabel.Low, first.ConfidenceLabel);
        Assert.Contains(MatchLocation.Semantic, first.MatchLocations ?? []);
        Assert.Contains("colonial school", first.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticSearch_MergesExactAndSemanticResults_WithLocations()
    {
        long semanticChunk = SeedBookChunk(
            "P11SEMANTIC00000000003",
            "Semantic-only book",
            "teacher training and classroom policy",
            rating: 5);
        var repository = new EmbeddingVectorRepository(_context);
        await repository.CreateAsync(NewVector(semanticChunk, [1.0f, 0.0f]), CancellationToken.None);
        var exact = new StubExactSearch(
            new CombinedSearchResult(
                "P11EXACT0000000000001",
                "Teacher title exact",
                "Author",
                100,
                ["title"],
                []));
        var service = new SemanticSearchService(
            _context,
            new StubOllamaProvider { QueryVector = [1.0f, 0.0f] },
            exact);

        SemanticSearchResponse response = await service.SearchAsync(
            "teacher",
            5,
            CancellationToken.None);

        Assert.False(response.UsedExactFallback);
        Assert.Contains(response.Results, result =>
            result.BookId == "P11SEMANTIC00000000003" &&
            result.MatchLocations?.Contains(MatchLocation.Semantic) == true &&
            result.HybridScore.HasValue);
        Assert.Contains(response.Results, result =>
            result.BookId == "P11EXACT0000000000001" &&
            result.MatchLocations?.Contains(MatchLocation.Title) == true &&
            result.HybridScore.HasValue);
    }

    [Fact]
    public async Task SemanticSearch_OllamaUnavailable_ReturnsExactFallback()
    {
        var exact = new StubExactSearch(
            new CombinedSearchResult(
                "P11FALLBACK00000000001",
                "Exact fallback book",
                "Author",
                12,
                ["Title"],
                []));
        var service = new SemanticSearchService(
            _context,
            new StubOllamaProvider { Available = false },
            exact);

        SemanticSearchResponse response = await service.SearchAsync(
            "fallback",
            5,
            CancellationToken.None);

        Assert.True(response.ProviderUnavailable);
        Assert.True(response.UsedExactFallback);
        SemanticSearchResult result = Assert.Single(response.Results);
        Assert.Equal("P11FALLBACK00000000001", result.BookId);
        Assert.True(result.ExactFallback);
        Assert.Null(result.SemanticScore);
    }

    private long SeedBookChunk(
        string bookId,
        string title,
        string chunkText,
        int? rating = null)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = title,
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.Indexed,
            EmbeddingStatus = (int)SearchEmbeddingStatus.Embedded,
            Rating = rating,
        });
        var chunk = new SearchChunkRow
        {
            BookId = bookId,
            ChunkIndex = 0,
            ChunkText = chunkText,
            Source = (int)SearchChunkSource.Page,
            TokenCount = SearchChunker.CountTokens(chunkText),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        _context.SearchChunks.Add(chunk);
        _context.SaveChanges();
        return chunk.ChunkId;
    }

    private static EmbeddingVectorRecord NewVector(long chunkId, float[] vector) =>
        new(
            Id: 0,
            ChunkId: chunkId,
            ModelName: EmbeddingGenerationService.DefaultModelName,
            ModelVersion: EmbeddingGenerationService.DefaultModelVersion,
            Vector: vector,
            DimensionCount: vector.Length,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

    private sealed class StubOllamaProvider : IOllamaEmbeddingProvider
    {
        public bool Available { get; init; } = true;

        public float[] QueryVector { get; init; } = [1f, 0f];

        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                QueryVector));

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Available);
    }

    private sealed class StubExactSearch : ICombinedSearchService
    {
        private readonly IReadOnlyList<CombinedSearchResult> _results;

        public StubExactSearch(params CombinedSearchResult[] results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<CombinedSearchResult>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(_results);
    }
}
