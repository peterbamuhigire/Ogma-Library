using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 WP5 Index Manager backend and rebuild reliability tests.
/// </summary>
public sealed class IndexManagerServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public IndexManagerServiceTests()
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
    public async Task IndexManager_GetStatus_ReturnsCountsAndPublishesEvent()
    {
        string indexedBook = SeedBook("P10INDEXSTATUS000000001", SearchBookIndexStatus.Indexed);
        string failedBook = SeedBook("P10INDEXSTATUS000000002", SearchBookIndexStatus.Failed);
        _context.ExtractedPages.AddRange(
            new ExtractedPageRow
            {
                BookId = indexedBook,
                PageNumber = 0,
                TextContent = "indexed page",
                ExtractionQuality = (int)SearchExtractionQuality.Scanned,
                WordCount = 2,
                ContentHash = "hash",
                ExtractionUtc = DateTimeOffset.UtcNow,
            },
            new ExtractedPageRow
            {
                BookId = failedBook,
                PageNumber = 0,
                TextContent = null,
                ExtractionQuality = (int)SearchExtractionQuality.Failed,
                WordCount = 0,
                ContentHash = "hash",
                ExtractionUtc = DateTimeOffset.UtcNow,
            });
        _context.SaveChanges();
        await SaveChunksAsync(indexedBook, "index manager searchable text");
        var service = new IndexManagerService(
            _context,
            new NoOpExtractionPipeline(),
            new FtsIndexService(_context));
        var observer = new RecordingObserver();
        using IDisposable subscription = service.Events.Subscribe(observer);

        IndexManagerStatus status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(2, status.TotalBooks);
        Assert.Equal(1, status.IndexedBooks);
        Assert.Equal(1, status.FailedBooks);
        Assert.Equal(1, status.PendingOcrPages);
        Assert.Equal(1, status.FailedExtractionPages);
        Assert.Equal(1, status.SearchChunkCount);
        Assert.True(status.IndexSizeBytes > 0);
        Assert.True(status.Integrity.IsHealthy, status.Integrity.ErrorMessage);
        Assert.Contains(status.Books, book => book.BookId == indexedBook && book.PendingOcrPageCount == 1);
        Assert.Contains(observer.Events, update => update is IndexStatusUpdate.StatusChanged);
    }

    [Fact]
    public async Task IndexRebuild_CompletesWithoutDuplicatesOrCorruption()
    {
        const int bookCount = 100;
        for (int i = 0; i < bookCount; i++)
        {
            string bookId = SeedBook($"P10G7BOOK{i:000000000000000}", SearchBookIndexStatus.Indexed);
            await SaveChunksAsync(bookId, $"before rebuild marker{i:000}");
            _context.ExtractedPages.Add(new ExtractedPageRow
            {
                BookId = bookId,
                PageNumber = 0,
                TextContent = $"before rebuild marker{i:000}",
                ExtractionQuality = (int)SearchExtractionQuality.Full,
                WordCount = 3,
                ContentHash = "old",
                ExtractionUtc = DateTimeOffset.UtcNow,
            });
        }

        _context.SaveChanges();
        int beforeCount = _context.SearchChunks.Count();
        var service = new IndexManagerService(
            _context,
            new DeterministicRebuildPipeline(_context),
            new FtsIndexService(_context));
        var observer = new RecordingObserver();
        using IDisposable subscription = service.Events.Subscribe(observer);

        IndexRebuildResult result = await service.RebuildAsync(CancellationToken.None);
        FtsIntegrityResult integrity = await new FtsIndexService(_context)
            .CheckIntegrityAsync(CancellationToken.None);

        Assert.True(result.Completed, result.ErrorMessage);
        Assert.Equal(bookCount, result.BooksAttempted);
        Assert.Equal(bookCount, result.BooksIndexed);
        Assert.Equal(beforeCount, _context.SearchChunks.Count());
        Assert.Equal(bookCount, _context.ExtractedPages.Count());
        Assert.True(integrity.IsHealthy, integrity.ErrorMessage);
        Assert.Contains(observer.Events, update => update is IndexStatusUpdate.RebuildStarted);
        Assert.Contains(observer.Events, update => update is IndexStatusUpdate.RebuildCompleted);
        Assert.Equal(bookCount, _context.Books.Count(book => book.IndexStatus == (int)SearchBookIndexStatus.Indexed));
    }

    [Fact]
    public async Task IndexRebuild_CancelledAfterReset_LeavesConsistentState()
    {
        string bookId = SeedBook("P10CANCELBOOK0000000001", SearchBookIndexStatus.Indexed);
        await SaveChunksAsync(bookId, "cancel rebuild old text");
        var service = new IndexManagerService(
            _context,
            new CancellingPipeline(),
            new FtsIndexService(_context));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RebuildAsync(CancellationToken.None));
        FtsIntegrityResult integrity = await new FtsIndexService(_context)
            .CheckIntegrityAsync(CancellationToken.None);

        Assert.Empty(_context.SearchChunks);
        Assert.Empty(_context.ExtractedPages);
        Assert.Equal((int)SearchBookIndexStatus.NotIndexed, _context.Books.Single(b => b.BookId == bookId).IndexStatus);
        Assert.True(integrity.IsHealthy, integrity.ErrorMessage);
    }

    private async Task SaveChunksAsync(string bookId, string text)
    {
        var repository = new SearchChunkRepository(_context);
        await repository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Page,
            [
                new SearchChunkRecord(
                    Id: 0,
                    BookId: bookId,
                    ExtractedPageId: null,
                    PageIndex: null,
                    ChunkIndex: 0,
                    Text: text,
                    TokenCount: SearchChunker.CountTokens(text),
                    Source: SearchChunkSource.Page,
                    CreatedAtUtc: DateTimeOffset.UtcNow),
            ],
            CancellationToken.None);
    }

    private string SeedBook(string bookId, SearchBookIndexStatus status)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = $"Index Manager {bookId}",
            Status = 0,
            IndexStatus = (int)status,
            Sha256Hash = "hash",
        });
        _context.SaveChanges();
        return bookId;
    }

    private sealed class RecordingObserver : IObserver<IndexStatusUpdate>
    {
        public List<IndexStatusUpdate> Events { get; } = [];

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(IndexStatusUpdate value) => Events.Add(value);
    }

    private sealed class NoOpExtractionPipeline : IExtractionPipelineService
    {
        public Task<ExtractionBookResult> IndexBookAsync(string bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBookResult(bookId, true, 0, 0, 0, 0, null));

        public Task<ExtractionBatchResult> IndexNextBatchAsync(int maxBooks, CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0));
    }

    private sealed class DeterministicRebuildPipeline : IExtractionPipelineService
    {
        private readonly CatalogueDbContext _context;

        public DeterministicRebuildPipeline(CatalogueDbContext context)
        {
            _context = context;
        }

        public Task<ExtractionBookResult> IndexBookAsync(string bookId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<ExtractionBatchResult> IndexNextBatchAsync(int maxBooks, CancellationToken cancellationToken)
        {
            List<BookRow> books = _context.Books
                .Where(book => book.Status == 0 && book.IndexStatus == (int)SearchBookIndexStatus.NotIndexed)
                .OrderBy(book => book.BookId)
                .Take(maxBooks)
                .ToList();
            if (books.Count == 0)
            {
                return new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0);
            }

            var repository = new SearchChunkRepository(_context);
            int chunks = 0;
            foreach (BookRow book in books)
            {
                string text = $"rebuilt text for {book.BookId}";
                _context.ExtractedPages.Add(new ExtractedPageRow
                {
                    BookId = book.BookId,
                    PageNumber = 0,
                    TextContent = text,
                    ExtractionQuality = (int)SearchExtractionQuality.Full,
                    WordCount = SearchChunker.CountTokens(text),
                    ContentHash = "hash",
                    ExtractionUtc = DateTimeOffset.UtcNow,
                });
                book.IndexStatus = (int)SearchBookIndexStatus.Indexed;
                await _context.SaveChangesAsync(cancellationToken);
                await repository.ReplaceForBookAsync(
                    book.BookId,
                    SearchChunkSource.Page,
                    [
                        new SearchChunkRecord(
                            Id: 0,
                            BookId: book.BookId,
                            ExtractedPageId: null,
                            PageIndex: null,
                            ChunkIndex: 0,
                            Text: text,
                            TokenCount: SearchChunker.CountTokens(text),
                            Source: SearchChunkSource.Page,
                            CreatedAtUtc: DateTimeOffset.UtcNow),
                    ],
                    cancellationToken);
                chunks++;
            }

            return new ExtractionBatchResult(books.Count, books.Count, 0, books.Count, 0, 0, chunks);
        }
    }

    private sealed class CancellingPipeline : IExtractionPipelineService
    {
        public Task<ExtractionBookResult> IndexBookAsync(string bookId, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();

        public Task<ExtractionBatchResult> IndexNextBatchAsync(int maxBooks, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }
}
