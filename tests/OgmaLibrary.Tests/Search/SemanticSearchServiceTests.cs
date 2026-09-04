using System.Diagnostics;
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
    public async Task SemanticSearch_ReusesBoundedInMemoryQueryEmbedding()
    {
        long chunkId = SeedBookChunk(
            "P25QUERYCACHE000000001",
            "Cached query book",
            "local embedding cache");
        await new EmbeddingVectorRepository(_context).CreateAsync(
            NewVector(chunkId, [1.0f, 0.0f]),
            CancellationToken.None);
        var provider = new StubOllamaProvider { QueryVector = [1.0f, 0.0f] };
        var service = new SemanticSearchService(_context, provider, new StubExactSearch());

        SemanticSearchResponse first = await service.SearchAsync(
            "same query",
            5,
            CancellationToken.None);
        SemanticSearchResponse second = await service.SearchAsync(
            " same query ",
            5,
            CancellationToken.None);

        Assert.False(first.EmbeddingCacheHit);
        Assert.True(second.EmbeddingCacheHit);
        Assert.Equal(1, provider.EmbedCalls);
    }

    [Fact]
    public async Task PerfBenchmark_SemanticSearch_P95_LessThan1500ms()
    {
        const int bookCount = 50_000;
        const int queryCount = 20;
        SeedPerformanceCorpus(bookCount);
        var service = new SemanticSearchService(
            _context,
            new StubOllamaProvider { QueryVector = [1.0f, 0.0f, 0.0f, 0.0f] },
            new StubExactSearch());

        _ = await service.SearchAsync("warmup query", 10, CancellationToken.None);

        var elapsed = new List<double>(queryCount);
        for (int i = 0; i < queryCount; i++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            SemanticSearchResponse response = await service.SearchAsync(
                $"semantic benchmark {i:00}",
                10,
                CancellationToken.None);
            stopwatch.Stop();

            Assert.NotEmpty(response.Results);
            elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        double p95 = elapsed
            .OrderBy(value => value)
            .ElementAt((int)Math.Ceiling(queryCount * 0.95) - 1);
        Assert.True(p95 <= 1500, $"Expected semantic search P95 <= 1500 ms, actual {p95:F2} ms");
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

    [Fact]
    public async Task SemanticSearch_DimensionMismatch_UsesExactFallback()
    {
        long chunkId = SeedBookChunk(
            "P26DIMENSIONBOOK000001",
            "Dimension Book",
            "dimension mismatch");
        await new EmbeddingVectorRepository(_context).CreateAsync(
            NewVector(chunkId, [1.0f, 0.0f]),
            CancellationToken.None);
        var service = new SemanticSearchService(
            _context,
            new StubOllamaProvider { QueryVector = [1.0f, 0.0f, 0.0f] },
            new StubExactSearch());

        SemanticSearchResponse response = await service.SearchAsync(
            "dimension query",
            5,
            CancellationToken.None);

        Assert.True(response.UsedExactFallback);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task SemanticSearch_TombstonedVector_IsNotUsedAsAnIndex()
    {
        long chunkId = SeedBookChunk(
            "P26TOMBSTONEDBOOK00001",
            "Tombstoned Book",
            "stale vector content");
        _context.EmbeddingVectors.Add(new EmbeddingVectorRow
        {
            ChunkId = chunkId,
            ModelName = EmbeddingGenerationService.DefaultModelName,
            ModelVersion = EmbeddingGenerationService.DefaultModelVersion,
            ProviderKey = EmbeddingGenerationService.DefaultProviderKey,
            DimensionCount = 2,
            VectorBlob = SerializeVector([1.0f, 0.0f]),
            SourceHash = new string('a', 64),
            IsTombstoned = true,
            TombstonedUtc = DateTimeOffset.UtcNow,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var service = new SemanticSearchService(
            _context,
            new StubOllamaProvider { QueryVector = [1.0f, 0.0f] },
            new StubExactSearch());

        SemanticSearchResponse response = await service.SearchAsync(
            "stale vector query",
            5,
            CancellationToken.None);

        Assert.False(response.ProviderUnavailable);
        Assert.True(response.UsedExactFallback);
        Assert.Equal(SemanticSearchAvailability.NoIndex, response.Availability);
        Assert.Empty(response.Results);
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

    private void SeedPerformanceCorpus(int bookCount)
    {
        for (int i = 0; i < bookCount; i++)
        {
            string bookId = $"P11PERF{i:000000000000000000}";
            _context.Books.Add(new BookRow
            {
                BookId = bookId,
                Title = $"Semantic Benchmark Book {i:0000}",
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                EmbeddingStatus = (int)SearchEmbeddingStatus.Embedded,
            });
            var chunk = new SearchChunkRow
            {
                BookId = bookId,
                ChunkIndex = 0,
                ChunkText = $"benchmark corpus book {i:0000}",
                Source = (int)SearchChunkSource.Page,
                TokenCount = 4,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            _context.SearchChunks.Add(chunk);
            _context.EmbeddingVectors.Add(new EmbeddingVectorRow
            {
                Chunk = chunk,
                ModelName = EmbeddingGenerationService.DefaultModelName,
                ModelVersion = EmbeddingGenerationService.DefaultModelVersion,
                DimensionCount = 4,
                VectorBlob = SerializeVector([
                    i == 0 ? 1.0f : 0.2f,
                    (i % 7) / 10.0f,
                    (i % 11) / 10.0f,
                    (i % 13) / 10.0f,
                ]),
                GeneratedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        _context.SaveChanges();
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

    private static byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private sealed class StubOllamaProvider : IOllamaEmbeddingProvider
    {
        public bool Available { get; init; } = true;

        public float[] QueryVector { get; init; } = [1f, 0f];

        public int EmbedCalls { get; private set; }

        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken)
        {
            EmbedCalls++;
            return Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                QueryVector));
        }

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
