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
        _context.Jobs.Add(new JobRow
        {
            JobType = "OcrJob",
            IdempotencyKey = "ocr-status-key",
            Status = 1,
            BookId = indexedBook,
            Payload = """{"FilePath":"scan.pdf","Language":"eng","TotalPages":12,"ProcessedPages":3}""",
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
        OcrJobStatusItem ocrJob = Assert.Single(status.OcrJobs);
        Assert.Equal(indexedBook, ocrJob.BookId);
        Assert.Equal(OcrJobState.Running, ocrJob.State);
        Assert.Equal(3, ocrJob.ProcessedPages);
        Assert.Equal(12, ocrJob.TotalPages);
        Assert.Equal(25, ocrJob.PercentComplete);
        Assert.True(status.SmartShelfStats.RequiredIndexesHealthy);
        Assert.True(status.SmartShelfStats.LastQueryMilliseconds >= 0);
        Assert.Contains(observer.Events, update => update is IndexStatusUpdate.StatusChanged);
    }

    [Fact]
    public async Task OcrJobControls_PauseCancelRetry_UpdateJobStatesAndPublishStatus()
    {
        string bookId = SeedBook("P15OCRCONTROLS00000001", SearchBookIndexStatus.Indexed);
        JobRow running = SeedOcrJob(bookId, "ocr-running", 1);
        JobRow pending = SeedOcrJob(bookId, "ocr-pending", 0);
        JobRow failed = SeedOcrJob(bookId, "ocr-failed", 3, "OCR failed");
        var service = new IndexManagerService(
            _context,
            new NoOpExtractionPipeline(),
            new FtsIndexService(_context));
        var observer = new RecordingObserver();
        using IDisposable subscription = service.Events.Subscribe(observer);

        await service.PauseOcrJobAsync(running.JobId, CancellationToken.None);
        await service.CancelOcrJobAsync(pending.JobId, CancellationToken.None);
        await service.RetryOcrJobAsync(failed.JobId, CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.Equal(5, _context.Jobs.Single(job => job.JobId == running.JobId).Status);
        Assert.Equal(4, _context.Jobs.Single(job => job.JobId == pending.JobId).Status);
        JobRow retried = _context.Jobs.Single(job => job.JobId == failed.JobId);
        Assert.Equal(0, retried.Status);
        Assert.Null(retried.ErrorMessage);
        Assert.Equal(1, retried.RetryCount);
        Assert.True(observer.Events.OfType<IndexStatusUpdate.StatusChanged>().Count() >= 3);
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
    public async Task SearchReadModel_RebuildPublishesLanReadyLifecycleEvents()
    {
        string bookId = SeedBook("P10READMODEL00000000001", SearchBookIndexStatus.Indexed);
        await SaveChunksAsync(bookId, "before read model rebuild marker");
        var service = new IndexManagerService(
            _context,
            new DeterministicRebuildPipeline(_context),
            new FtsIndexService(_context));
        var observer = new SearchReadModelObserver();
        using IDisposable subscription = ((ISearchReadModel)service).Events.Subscribe(observer);

        IndexRebuildResult result = await service.RebuildAsync(CancellationToken.None);

        Assert.True(result.Completed, result.ErrorMessage);
        Assert.Contains(observer.Events, update =>
            update is SearchIndexEvent.BookIndexed indexed &&
            indexed.BookId == bookId &&
            indexed.ChunkCount == 1);
        Assert.Contains(observer.Events, update =>
            update is SearchIndexEvent.IndexRebuilt rebuilt &&
            rebuilt.TotalChunks == 1 &&
            rebuilt.DurationMs >= 0);
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

    private JobRow SeedOcrJob(string bookId, string key, int status, string? error = null)
    {
        var job = new JobRow
        {
            BookId = bookId,
            JobType = "OcrJob",
            IdempotencyKey = key,
            Status = status,
            Payload = """{"FilePath":"scan.pdf","Language":"eng","TotalPages":4,"ProcessedPages":1}""",
            StartedUtc = status == 1 ? DateTimeOffset.UtcNow.AddMinutes(-1) : null,
            CompletedUtc = status == 3 ? DateTimeOffset.UtcNow : null,
            ErrorMessage = error,
        };
        _context.Jobs.Add(job);
        _context.SaveChanges();
        return job;
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

    private sealed class SearchReadModelObserver : IObserver<SearchIndexEvent>
    {
        public List<SearchIndexEvent> Events { get; } = [];

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(SearchIndexEvent value) => Events.Add(value);
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
