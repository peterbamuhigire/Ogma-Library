using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP2 embedding-generation pipeline tests.
/// </summary>
public sealed class EmbeddingGenerationServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public EmbeddingGenerationServiceTests()
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
    public async Task EmbeddingGeneration_Idempotent_SameModelVersion()
    {
        const string bookId = "P11PIPELINE00000000001";
        SeedBookWithChunks(bookId, "first semantic chunk", "second semantic chunk");
        var provider = new StubOllamaProvider();
        var service = CreateService(provider);
        var observer = new SemanticObserver();
        using IDisposable subscription = ((ISemanticSearchReadModel)service).Events.Subscribe(observer);

        EmbeddingGenerationBatchResult first = await service.GenerateNextBatchAsync(10, CancellationToken.None);
        EmbeddingGenerationBatchResult second = await service.GenerateNextBatchAsync(10, CancellationToken.None);

        Assert.Equal(2, first.ChunksAttempted);
        Assert.Equal(2, first.ChunksEmbedded);
        Assert.Equal(0, first.ChunksFailed);
        Assert.Equal(0, second.ChunksAttempted);
        Assert.Equal(2, _context.EmbeddingVectors.Count());
        Assert.All(_context.EmbeddingVectors, vector =>
        {
            Assert.Equal(64, vector.SourceHash.Length);
            Assert.Equal(SearchChunker.CurrentVersion, vector.ChunkerVersion);
            Assert.Equal("ollama", vector.ProviderKey);
        });
        Assert.Equal((int)SearchEmbeddingStatus.Embedded, _context.Books.Single(b => b.BookId == bookId).EmbeddingStatus);
        Assert.Equal(2, observer.Events.OfType<SemanticIndexEvent.EmbeddingGenerated>().Count());
        Assert.Contains(observer.Events, e =>
            e is SemanticIndexEvent.EmbeddingGenerated generated &&
            generated.TotalEmbedded == 2 &&
            generated.TotalChunks == 2);
    }

    [Fact]
    public async Task EmbeddingGeneration_OllamaUnavailable_DegradesWithoutRows()
    {
        const string bookId = "P11UNAVAILABLE00000001";
        SeedBookWithChunks(bookId, "waiting for local ollama");
        var provider = new StubOllamaProvider { Available = false };
        var service = CreateService(provider);
        var observer = new SemanticObserver();
        using IDisposable subscription = ((ISemanticSearchReadModel)service).Events.Subscribe(observer);

        EmbeddingGenerationBatchResult result = await service.GenerateNextBatchAsync(10, CancellationToken.None);

        Assert.True(result.ProviderUnavailable);
        Assert.Equal(0, result.ChunksAttempted);
        Assert.Empty(_context.EmbeddingVectors);
        Assert.Equal((int)SearchEmbeddingStatus.NotEmbedded, _context.Books.Single(b => b.BookId == bookId).EmbeddingStatus);
        Assert.Contains(observer.Events, e => e is SemanticIndexEvent.OllamaUnavailable);
    }

    [Fact]
    public async Task EmbeddingGeneration_ChunkFailure_RecordsJobAndContinues()
    {
        const string bookId = "P11FAILCONTINUE0000001";
        SeedBookWithChunks(bookId, "good semantic chunk", "bad semantic chunk");
        var provider = new StubOllamaProvider { FailText = "bad" };
        var service = CreateService(provider);
        var observer = new SemanticObserver();
        using IDisposable subscription = ((ISemanticSearchReadModel)service).Events.Subscribe(observer);

        EmbeddingGenerationBatchResult result = await service.GenerateNextBatchAsync(10, CancellationToken.None);

        Assert.Equal(2, result.ChunksAttempted);
        Assert.Equal(1, result.ChunksEmbedded);
        Assert.Equal(1, result.ChunksFailed);
        Assert.Single(_context.EmbeddingVectors);
        Assert.Contains(_context.Jobs, job =>
            job.JobType == "EmbeddingFailed" &&
            job.BookId == bookId &&
            job.Status == 3);
        Assert.Equal((int)SearchEmbeddingStatus.Failed, _context.Books.Single(b => b.BookId == bookId).EmbeddingStatus);
        Assert.Contains(observer.Events, e => e is SemanticIndexEvent.EmbeddingFailed);
    }

    [Fact]
    public async Task EmbeddingGeneration_NonLocalProvider_IsRejectedWithoutCallingProvider()
    {
        const string bookId = "P25NONLOCAL000000000001";
        SeedBookWithChunks(bookId, "must remain local");
        var provider = new StubOllamaProvider { IsLocal = false };
        var service = CreateService(provider);

        EmbeddingGenerationBatchResult result = await service.GenerateNextBatchAsync(10, CancellationToken.None);

        Assert.True(result.ProviderUnavailable);
        Assert.Equal(0, provider.EmbedCallCount);
        Assert.Empty(_context.EmbeddingVectors);
    }

    [Fact]
    public async Task EmbeddingGeneration_InvalidVector_IsRejectedAndRecorded()
    {
        const string bookId = "P25INVALIDVECTOR000001";
        SeedBookWithChunks(bookId, "invalid vector");
        var service = CreateService(new StubOllamaProvider { InvalidVector = true });

        EmbeddingGenerationBatchResult result = await service.GenerateNextBatchAsync(10, CancellationToken.None);

        Assert.Equal(1, result.ChunksFailed);
        Assert.Empty(_context.EmbeddingVectors);
        Assert.Contains(_context.Jobs, job => job.JobType == "EmbeddingFailed" && job.BookId == bookId);
    }

    private EmbeddingGenerationService CreateService(IOllamaEmbeddingProvider provider) =>
        new(
            _context,
            provider,
            new EmbeddingVectorRepository(_context));

    private void SeedBookWithChunks(string bookId, params string[] chunkTexts)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = $"Embedding {bookId}",
            Status = 0,
            IndexStatus = (int)SearchBookIndexStatus.Indexed,
            EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded,
        });

        for (int i = 0; i < chunkTexts.Length; i++)
        {
            _context.SearchChunks.Add(new SearchChunkRow
            {
                BookId = bookId,
                ChunkIndex = i,
                ChunkText = chunkTexts[i],
                Source = (int)SearchChunkSource.Page,
                TokenCount = SearchChunker.CountTokens(chunkTexts[i]),
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        _context.SaveChanges();
    }

    private sealed class StubOllamaProvider : IOllamaEmbeddingProvider
    {
        public bool Available { get; init; } = true;

        public bool IsLocal { get; init; } = true;

        public bool InvalidVector { get; init; }

        public int EmbedCallCount { get; private set; }

        public string? FailText { get; init; }

        public string ProviderKey => "ollama";

        public bool IsLocalOnly => IsLocal;

        public Task<OllamaEmbeddingResult> EmbedAsync(
            string text,
            string modelName,
            CancellationToken cancellationToken)
        {
            EmbedCallCount++;
            if (FailText is not null && text.Contains(FailText, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Embedding fixture failure.");
            }

            return Task.FromResult(new OllamaEmbeddingResult(
                modelName,
                EmbeddingGenerationService.DefaultModelVersion,
                InvalidVector
                    ? [float.NaN]
                    : [text.Length, SearchChunker.CountTokens(text), 1f]));
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Available);
    }

    private sealed class SemanticObserver : IObserver<SemanticIndexEvent>
    {
        public List<SemanticIndexEvent> Events { get; } = [];

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(SemanticIndexEvent value) => Events.Add(value);
    }
}
