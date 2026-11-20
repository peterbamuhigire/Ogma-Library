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
            TestEmbeddingIdentity.ModelId,
            TestEmbeddingIdentity.ModelVersion,
            CancellationToken.None);
        EmbeddingVectorRecord? loadedStaging = await repository.GetForChunkAsync(
            chunkId,
            TestEmbeddingIdentity.ModelId,
            TestEmbeddingIdentity.ModelVersion,
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

    [Fact]
    public async Task ModelSwitch_KeepsOldGenerationServing_ThenSwapsAtomically()
    {
        string[] books = ["P25SWITCH0000000000001", "P25SWITCH0000000000002", "P25SWITCH0000000000003"];
        foreach (string book in books)
        {
            SeedBookWithChunk(book, "regeneration chunk for " + book);
        }

        var oldModel = new NamedProvider("ollama", "old-model", "old-model:1", 3);
        var newModel = new NamedProvider("builtin-onnx", "new-model", "rev-2", 5);
        var provider = new SwitchableProvider(oldModel);
        using var lifecycle = new EmbeddingIndexLifecycleService(_context);
        SemanticGenerationCoordinator coordinator = Coordinator(provider, lifecycle);
        await DrainAsync(coordinator);
        Assert.Equal(3, _context.EmbeddingVectors.Count(row => row.IndexVersion == "fts5-v1" && row.ModelName == "old-model"));

        provider.Current = newModel;
        EmbeddingGenerationBatchResult first = await coordinator.GenerateNextBatchAsync(1, CancellationToken.None);
        EmbeddingIndexState midway = await lifecycle.GetStateAsync(CancellationToken.None);

        Assert.Equal(1, first.ChunksEmbedded);
        Assert.Equal("fts5-v1", midway.ActiveIndexVersion);
        Assert.NotNull(midway.StagingIndexVersion);

        // Old provider still available: searches keep serving the old generation.
        SemanticSearchResponse served = await Search(provider, lifecycle, new Catalog(oldModel));
        Assert.Equal(SemanticSearchAvailability.Ready, served.Availability);
        Assert.Equal(3, served.Results.Count);

        // Old provider gone: semantic is "preparing" (NoIndex), never mixed or partial.
        SemanticSearchResponse preparing = await Search(provider, lifecycle, new Catalog());
        Assert.Equal(SemanticSearchAvailability.NoIndex, preparing.Availability);

        string staging = midway.StagingIndexVersion!;
        while (true)
        {
            await coordinator.GenerateNextBatchAsync(1, CancellationToken.None);
            EmbeddingIndexState state = await lifecycle.GetStateAsync(CancellationToken.None);
            if (state.ActiveIndexVersion == "fts5-v1")
            {
                Assert.Equal(staging, state.StagingIndexVersion);
                continue;
            }

            // The pointer moved in one step, and only once coverage was complete.
            Assert.Equal(staging, state.ActiveIndexVersion);
            Assert.Null(state.StagingIndexVersion);
            Assert.Equal(3, _context.EmbeddingVectors.Count(row => row.IndexVersion == staging && row.ModelName == "new-model"));
            break;
        }

        SemanticSearchResponse swapped = await Search(provider, lifecycle, new Catalog());
        Assert.Equal(SemanticSearchAvailability.Ready, swapped.Availability);
        Assert.Equal(3, swapped.Results.Count);
    }

    [Fact]
    public async Task ProviderSwitchedMidGeneration_LeavesNoMixedDimensionVectors()
    {
        for (int i = 1; i <= 4; i++)
        {
            SeedBookWithChunk($"P25MIXED00000000000000{i}", $"mixed generation chunk {i}");
        }

        var modelA = new NamedProvider("ollama", "model-a", "a:1", 3);
        var modelB = new NamedProvider("builtin-onnx", "model-b", "b:1", 5);
        var modelC = new NamedProvider("builtin-onnx", "model-c", "c:1", 7);
        var provider = new SwitchableProvider(modelA);
        using var lifecycle = new EmbeddingIndexLifecycleService(_context);
        SemanticGenerationCoordinator coordinator = Coordinator(provider, lifecycle);
        await DrainAsync(coordinator);

        provider.Current = modelB;
        await coordinator.GenerateNextBatchAsync(1, CancellationToken.None);
        string abandoned = (await lifecycle.GetStateAsync(CancellationToken.None)).StagingIndexVersion!;

        // Switch to C while a B batch is running.
        provider.AfterEmbed = () => provider.Current = modelC;
        EmbeddingGenerationBatchResult interrupted = await coordinator.GenerateNextBatchAsync(4, CancellationToken.None);
        provider.AfterEmbed = null;
        Assert.True(interrupted.ProviderChanged);
        Assert.Equal(0, interrupted.ChunksFailed);

        await DrainAsync(coordinator);
        EmbeddingIndexState final = await lifecycle.GetStateAsync(CancellationToken.None);

        Assert.Null(final.StagingIndexVersion);
        Assert.Empty(_context.EmbeddingVectors.Where(row => row.IndexVersion == abandoned));
        Assert.Empty(_context.EmbeddingVectors.Where(row => row.ModelName == "model-b"));
        Assert.Equal(4, _context.EmbeddingVectors.Count(row => row.IndexVersion == final.ActiveIndexVersion && row.ModelName == "model-c"));
        foreach (var generation in _context.EmbeddingVectors.AsNoTracking().ToList().GroupBy(row => row.IndexVersion))
        {
            Assert.Single(generation.Select(row => row.DimensionCount).Distinct());
            Assert.Single(generation.Select(row => (row.ProviderKey, row.ModelName, row.ModelVersion)).Distinct());
        }
    }

    [Fact]
    public async Task InPlaceGeneration_TargetsTheActiveGeneration_NotFts5V1()
    {
        long first = SeedBookWithChunk("P25INPLACE000000000001", "promoted generation chunk");
        var model = new NamedProvider("builtin-onnx", "model-a", "a:1", 3);
        using var lifecycle = new EmbeddingIndexLifecycleService(_context);
        var repository = new EmbeddingVectorRepository(_context);
        await repository.CreateAsync(
            new EmbeddingVectorRecord(0, first, "model-a", "a:1", [1f, 1f, 1f], 3, DateTimeOffset.UtcNow, new string('a', 64),
                IndexVersion: "semantic-rebuild-promoted", ProviderKey: "builtin-onnx"),
            CancellationToken.None);
        await lifecycle.BeginRebuildAsync("semantic-rebuild-promoted", CancellationToken.None);
        await lifecycle.PromoteAsync("semantic-rebuild-promoted", CancellationToken.None);
        SeedBookWithChunk("P25INPLACE000000000002", "chunk added after promotion");

        await DrainAsync(Coordinator(new SwitchableProvider(model), lifecycle));

        Assert.Empty(_context.EmbeddingVectors.Where(row => row.IndexVersion == "fts5-v1"));
        Assert.Equal(2, _context.EmbeddingVectors.Count(row => row.IndexVersion == "semantic-rebuild-promoted"));
        Assert.Null((await lifecycle.GetStateAsync(CancellationToken.None)).StagingIndexVersion);
    }

    [Fact]
    public async Task Progress_CountsTheTargetGeneration_AndEstimatesFromTheRecentRate()
    {
        for (int i = 1; i <= 4; i++)
        {
            SeedBookWithChunk($"P25PROGRESS00000000000{i}", $"progress chunk {i}");
        }

        var clock = new SteppingClock();
        var generation = new EmbeddingGenerationService(
            _context,
            new NamedProvider("builtin-onnx", "model-a", "a:1", 3),
            new EmbeddingVectorRepository(_context))
        { Clock = clock };

        SemanticIndexProgress before = await generation.GetProgressAsync(CancellationToken.None);
        Assert.Equal(new SemanticIndexProgress(0, 4, 4, null, false), before);

        await generation.GenerateNextBatchAsync(2, CancellationToken.None);
        SemanticIndexProgress after = await generation.GetProgressAsync(CancellationToken.None);

        Assert.Equal(2, after.ChunksEmbedded);
        Assert.Equal(4, after.ChunksTotal);
        Assert.Equal(2, after.BooksWaiting);
        Assert.False(after.IsRegenerating);
        // Two embeddings ten seconds apart (one each tick), read ten seconds later: 0.1/s.
        Assert.Equal(TimeSpan.FromSeconds(20), after.EstimatedRemaining);
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
            TestEmbeddingIdentity.ModelId,
            TestEmbeddingIdentity.ModelVersion,
            vector,
            vector.Length,
            DateTimeOffset.UtcNow,
            new string('a', 64),
            IndexVersion: indexVersion);

    private sealed class StubEmbeddingProvider : FakeEmbeddingProvider
    {
        public bool Available { get; set; } = true;

        public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Available);

        protected override float[] CreateVector(string text, EmbeddingInputKind kind) =>
            [text.Length, SearchChunker.CountTokens(text), 1f];
    }

    private sealed class QueryEmbeddingProvider(float[] queryVector) : FakeEmbeddingProvider
    {
        public override int Dimensions => queryVector.Length;

        protected override float[] CreateVector(string text, EmbeddingInputKind kind) => queryVector;
    }

    private SemanticGenerationCoordinator Coordinator(IEmbeddingProvider provider, EmbeddingIndexLifecycleService lifecycle)
    {
        var generation = new EmbeddingGenerationService(_context, provider, new EmbeddingVectorRepository(_context));
        return new SemanticGenerationCoordinator(
            _context,
            provider,
            generation,
            lifecycle,
            new EmbeddingIndexRebuildService(generation, lifecycle));
    }

    private static async Task DrainAsync(SemanticGenerationCoordinator coordinator)
    {
        for (int i = 0; i < 50; i++)
        {
            EmbeddingGenerationBatchResult batch = await coordinator.GenerateNextBatchAsync(1, CancellationToken.None);
            Assert.False(batch.ProviderUnavailable);
            Assert.Equal(0, batch.ChunksFailed);
            if (batch.ChunksAttempted == 0 && !batch.ProviderChanged)
            {
                EmbeddingGenerationBatchResult settled = await coordinator.GenerateNextBatchAsync(1, CancellationToken.None);
                if (settled.ChunksAttempted == 0 && !settled.ProviderChanged)
                {
                    return;
                }
            }
        }

        Assert.Fail("Generation did not settle within 50 batches.");
    }

    private Task<SemanticSearchResponse> Search(
        IEmbeddingProvider provider,
        EmbeddingIndexLifecycleService lifecycle,
        IEmbeddingProviderCatalog catalog) =>
        new SemanticSearchService(
                _context,
                provider,
                new EmptyExactSearch(),
                indexLifecycle: lifecycle,
                providers: catalog)
            .SearchAsync("regeneration chunk", 10, CancellationToken.None);

    private sealed class NamedProvider(string key, string model, string version, int dimensions) : FakeEmbeddingProvider
    {
        public override string ProviderKey => key;

        public override string ModelId => model;

        public override string ModelVersion => version;

        public override int Dimensions => dimensions;

        protected override float[] CreateVector(string text, EmbeddingInputKind kind) =>
            [.. Enumerable.Range(0, dimensions).Select(index => 1f + (index * 0.1f) + (text.Length % 3))];
    }

    private sealed class SwitchableProvider(IEmbeddingProvider initial) : IEmbeddingProvider
    {
        public IEmbeddingProvider Current { get; set; } = initial;

        public Action? AfterEmbed { get; set; }

        public string ProviderKey => Current.ProviderKey;

        public bool IsLocalOnly => true;

        public string ModelId => Current.ModelId;

        public string ModelVersion => Current.ModelVersion;

        public int Dimensions => Current.Dimensions;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public async Task<EmbeddingResult> EmbedAsync(string text, EmbeddingInputKind kind, CancellationToken cancellationToken)
        {
            EmbeddingResult result = await Current.EmbedAsync(text, kind, cancellationToken);
            AfterEmbed?.Invoke();
            return result;
        }
    }

    private sealed class Catalog(params IEmbeddingProvider[] available) : IEmbeddingProviderCatalog
    {
        public IEmbeddingProvider? FindAvailable(string providerKey, string modelId, string modelVersion) =>
            available.FirstOrDefault(provider =>
                provider.ProviderKey == providerKey && provider.ModelId == modelId && provider.ModelVersion == modelVersion);
    }

    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

        // Every read advances ten seconds, so each embedding and each estimate is a tick apart.
        public override DateTimeOffset GetUtcNow()
        {
            _now = _now.AddSeconds(10);
            return _now;
        }
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
