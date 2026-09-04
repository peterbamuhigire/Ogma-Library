using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 25 side-by-side semantic-index lifecycle verification.</summary>
public sealed class Phase25EmbeddingIndexLifecycleTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase25EmbeddingIndexLifecycleTests()
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
    public async Task Repository_AllowsTwoIndexGenerations_AndReadsRequestedGeneration()
    {
        long chunkId = SeedBookWithChunk("P25LIFECYCLE0000000001", "generation-aware chunk");
        var repository = new EmbeddingVectorRepository(_context);

        EmbeddingVectorRecord active = await repository.CreateAsync(
            Vector(chunkId, "fts5-v1", [1f, 0f, 0f]),
            CancellationToken.None);
        EmbeddingVectorRecord staging = await repository.CreateAsync(
            Vector(chunkId, "semantic-rebuild-test", [0f, 1f, 0f]),
            CancellationToken.None);

        Assert.NotEqual(active.Id, staging.Id);
        EmbeddingVectorRecord? loadedActive = await repository.GetForChunkAsync(
            chunkId,
            EmbeddingGenerationService.DefaultModelName,
            EmbeddingGenerationService.DefaultModelVersion,
            CancellationToken.None);
        EmbeddingVectorRecord? loadedStaging = await repository.GetForChunkAsync(
            chunkId,
            EmbeddingGenerationService.DefaultModelName,
            EmbeddingGenerationService.DefaultModelVersion,
            CancellationToken.None,
            "semantic-rebuild-test");

        Assert.NotNull(loadedActive);
        Assert.NotNull(loadedStaging);
        Assert.Equal("fts5-v1", loadedActive.IndexVersion);
        Assert.Equal("semantic-rebuild-test", loadedStaging.IndexVersion);
    }

    [Fact]
    public async Task Lifecycle_PersistsResumeToken_AndPromotesAtomically()
    {
        var lifecycle = new EmbeddingIndexLifecycleService(_context);

        EmbeddingIndexState started = await lifecycle.BeginRebuildAsync(
            "semantic-rebuild-lifecycle",
            CancellationToken.None);
        EmbeddingIndexState resumed = await lifecycle.BeginRebuildAsync(
            "semantic-rebuild-lifecycle",
            CancellationToken.None);

        Assert.Equal("fts5-v1", started.ActiveIndexVersion);
        Assert.Equal("semantic-rebuild-lifecycle", resumed.StagingIndexVersion);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.BeginRebuildAsync(
            "semantic-rebuild-other",
            CancellationToken.None));

        EmbeddingIndexState promoted = await lifecycle.PromoteAsync(
            "semantic-rebuild-lifecycle",
            CancellationToken.None);

        Assert.Equal("semantic-rebuild-lifecycle", promoted.ActiveIndexVersion);
        Assert.Null(promoted.StagingIndexVersion);
        Assert.Equal(promoted, await lifecycle.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Rebuild_UnavailableProviderRetainsStaging_ThenResumesWithoutTouchingActive()
    {
        const string bookId = "P25RESUME0000000000001";
        SeedBookWithChunk(bookId, "first staged chunk");
        SeedBookWithChunk(bookId, "second staged chunk");
        var provider = new StubEmbeddingProvider { Available = false };
        var generation = new EmbeddingGenerationService(
            _context,
            provider,
            new EmbeddingVectorRepository(_context));
        var lifecycle = new EmbeddingIndexLifecycleService(_context);
        var rebuild = new EmbeddingIndexRebuildService(generation, lifecycle);

        EmbeddingIndexRebuildResult unavailable = await rebuild.RebuildAsync(1, CancellationToken.None);
        EmbeddingIndexState waiting = await lifecycle.GetStateAsync(CancellationToken.None);

        Assert.False(unavailable.Completed);
        Assert.True(unavailable.ProviderUnavailable);
        Assert.Equal("fts5-v1", unavailable.ActiveIndexVersion);
        Assert.Equal(unavailable.StagingIndexVersion, waiting.StagingIndexVersion);
        Assert.Empty(_context.EmbeddingVectors);

        provider.Available = true;
        EmbeddingIndexRebuildResult completed = await rebuild.RebuildAsync(1, CancellationToken.None);
        EmbeddingIndexState active = await lifecycle.GetStateAsync(CancellationToken.None);

        Assert.True(completed.Completed);
        Assert.Equal(completed.ActiveIndexVersion, active.ActiveIndexVersion);
        Assert.Null(active.StagingIndexVersion);
        Assert.Equal(2, _context.EmbeddingVectors.Count());
        Assert.All(_context.EmbeddingVectors, row => Assert.Equal(active.ActiveIndexVersion, row.IndexVersion));
    }

    [Fact]
    public async Task SemanticSearch_UsesActiveGeneration_AndIgnoresOldVectors()
    {
        long oldChunk = SeedBookWithChunk("P25ACTIVEOLD00000000001", "old generation");
        long stagedChunk = SeedBookWithChunk("P25ACTIVENEW0000000001", "new generation");
        var repository = new EmbeddingVectorRepository(_context);
        await repository.CreateAsync(Vector(oldChunk, "fts5-v1", [0f, 1f, 0f]), CancellationToken.None);
        await repository.CreateAsync(Vector(stagedChunk, "semantic-active-test", [1f, 0f, 0f]), CancellationToken.None);

        var lifecycle = new EmbeddingIndexLifecycleService(_context);
        await lifecycle.BeginRebuildAsync("semantic-active-test", CancellationToken.None);
        await lifecycle.PromoteAsync("semantic-active-test", CancellationToken.None);
        var service = new SemanticSearchService(
            _context,
            new QueryEmbeddingProvider([0f, 1f, 0f]),
            new EmptyExactSearch(),
            indexLifecycle: lifecycle);

        SemanticSearchResponse response = await service.SearchAsync(
            "active generation only",
            5,
            CancellationToken.None);

        Assert.Contains(response.Results, result => result.BookId == "P25ACTIVENEW0000000001");
        Assert.DoesNotContain(response.Results, result => result.BookId == "P25ACTIVEOLD00000000001");
    }

    private long SeedBookWithChunk(string bookId, string text)
    {
        if (!_context.Books.Any(book => book.BookId == bookId))
        {
            _context.Books.Add(new BookRow
            {
                BookId = bookId,
                Title = bookId,
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded,
            });
            _context.SaveChanges();
        }

        var chunk = new SearchChunkRow
        {
            BookId = bookId,
            ChunkIndex = _context.SearchChunks.Count(row => row.BookId == bookId),
            ChunkText = text,
            Source = (int)SearchChunkSource.Page,
            TokenCount = SearchChunker.CountTokens(text),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        _context.SearchChunks.Add(chunk);
        _context.SaveChanges();
        return chunk.ChunkId;
    }

    private static EmbeddingVectorRecord Vector(long chunkId, string indexVersion, float[] vector) =>
        new(
            0,
            chunkId,
            EmbeddingGenerationService.DefaultModelName,
            EmbeddingGenerationService.DefaultModelVersion,
            vector,
            vector.Length,
            DateTimeOffset.UtcNow,
            new string('a', 64),
            IndexVersion: indexVersion);

    private sealed class StubEmbeddingProvider : IOllamaEmbeddingProvider
    {
        public bool Available { get; set; } = true;

        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Available);

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                [text.Length, SearchChunker.CountTokens(text), 1f]));
    }

    private sealed class QueryEmbeddingProvider(float[] queryVector) : IOllamaEmbeddingProvider
    {
        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                queryVector));
    }

    private sealed class EmptyExactSearch : ICombinedSearchService
    {
        public Task<IReadOnlyList<CombinedSearchResult>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CombinedSearchResult>>([]);
    }
}
