using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>
/// Sept-23 Phase 17 (K21): honest text status, OCR after ingest, typed OCR failures and the
/// automatic OCR policy. Tests marked "fails before" reproduce the audit's defects.
/// </summary>
public sealed class Sept23Phase17TextStatusTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ogma-p17-lib-{Guid.NewGuid():N}");

    public Sept23Phase17TextStatusTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ── Policy (tasks 1, 2, 7) ────────────────────────────────────────────────────

    [Fact]
    public void TextStatus_AllPagesWithText_IsSearchable()
    {
        BookTextAssessment result = BookTextStatusPolicy.Assess(Pages(10, SearchExtractionQuality.Full, words: 200));

        Assert.Equal(BookTextStatus.Searchable, result.Status);
        Assert.Equal(1.0, result.TextQuality, 3);
        Assert.Null(result.OcrConfidence);
    }

    [Fact]
    public void TextStatus_AllPagesScanned_IsImageOnly()
    {
        BookTextAssessment result = BookTextStatusPolicy.Assess(Pages(3, SearchExtractionQuality.Scanned, words: 0));

        Assert.Equal(BookTextStatus.ImageOnly, result.Status);
        Assert.Equal(0, result.TextQuality);
        Assert.Equal(3, result.PagesNeedingOcr);
        Assert.True(BookTextStatusPolicy.NeedsOcr(result.Status));
    }

    [Fact]
    public void TextStatus_ImageOnlyThreshold_IsEightyPercentOfPages()
    {
        // 8 scanned of 10 pages: image only; 7 of 10: partly searchable.
        BookPageTextEvidence[] eight = [.. Pages(8, SearchExtractionQuality.Scanned, 0), .. Pages(2, SearchExtractionQuality.Full, 80, start: 8)];
        BookPageTextEvidence[] seven = [.. Pages(7, SearchExtractionQuality.Scanned, 0), .. Pages(3, SearchExtractionQuality.Full, 80, start: 7)];

        Assert.Equal(BookTextStatus.ImageOnly, BookTextStatusPolicy.Assess(eight).Status);
        Assert.Equal(BookTextStatus.PartlySearchable, BookTextStatusPolicy.Assess(seven).Status);
        Assert.Equal(0.3, BookTextStatusPolicy.Assess(seven).TextQuality, 3);
    }

    [Fact]
    public void TextStatus_BlankPagesInATextBook_StaySearchable()
    {
        // Blank pages (no text and no image) are normal; only scanned images make a book partial.
        BookPageTextEvidence[] pages = [.. Pages(18, SearchExtractionQuality.Full, 150), .. Pages(2, SearchExtractionQuality.Empty, 0, start: 18)];

        Assert.Equal(BookTextStatus.Searchable, BookTextStatusPolicy.Assess(pages).Status);
    }

    [Fact]
    public void TextStatus_OcrSelectedForScannedPages_IsOcrTextWithConfidence()
    {
        BookPageTextEvidence[] pages =
        [
            new(0, SearchExtractionQuality.Scanned, 0, HasOcrRow: true, OcrSelected: true, OcrConfidence: 0.9),
            new(1, SearchExtractionQuality.Scanned, 0, HasOcrRow: true, OcrSelected: true, OcrConfidence: 0.8),
        ];

        BookTextAssessment result = BookTextStatusPolicy.Assess(pages, BookOcrJobState.Completed);

        Assert.Equal(BookTextStatus.OcrText, result.Status);
        Assert.Equal(0.85, result.OcrConfidence!.Value, 3);
        Assert.Equal(0.85, result.TextQuality, 3);
    }

    [Fact]
    public void TextStatus_OcrRanButReadNothing_IsNoText()
    {
        BookPageTextEvidence[] pages = [new(0, SearchExtractionQuality.Scanned, 0, HasOcrRow: true, OcrSelected: false, OcrConfidence: 0.1)];

        Assert.Equal(BookTextStatus.NoText, BookTextStatusPolicy.Assess(pages, BookOcrJobState.Completed).Status);
    }

    [Fact]
    public void TextStatus_JobStates_OverlayInProgressAndFailure()
    {
        BookPageTextEvidence[] pages = [.. Pages(2, SearchExtractionQuality.Scanned, 0)];

        Assert.Equal(BookTextStatus.OcrInProgress, BookTextStatusPolicy.Assess(pages, BookOcrJobState.Active).Status);
        Assert.Equal(BookTextStatus.OcrFailed, BookTextStatusPolicy.Assess(pages, BookOcrJobState.Failed).Status);
        Assert.Equal(BookTextStatus.ImageOnly, BookTextStatusPolicy.Assess(pages, BookOcrJobState.Cancelled).Status);
    }

    [Fact]
    public void TextStatus_NoPagesOrOnlyFailedPages()
    {
        Assert.Equal(BookTextStatus.Unknown, BookTextStatusPolicy.Assess([]).Status);
        Assert.Equal(BookTextStatus.NoText, BookTextStatusPolicy.Assess(Pages(2, SearchExtractionQuality.Failed, 0)).Status);
    }

    [Fact]
    public void Projection_KnownTextStatus_ReplacesIndexedBadge()
    {
        // Fails before: an index job that finished on a scanned book showed "Indexed" (K21).
        var scanned = new CatalogueProcessingProjection(
            SearchBookIndexStatus.Indexed,
            SearchEmbeddingStatus.NotEmbedded,
            0,
            IsOcrDerived: false,
            TextStatus: BookTextStatus.ImageOnly);
        var legacy = new CatalogueProcessingProjection(SearchBookIndexStatus.Indexed, SearchEmbeddingStatus.NotEmbedded, 0, false);

        Assert.False(scanned.IsIndexed);
        Assert.True(scanned.IsImageOnly);
        Assert.True(scanned.NeedsOcr);
        Assert.True(legacy.IsIndexed);
        Assert.Equal(92, new CatalogueProcessingProjection(
            SearchBookIndexStatus.Indexed, SearchEmbeddingStatus.NotEmbedded, 0, true, BookTextStatus.OcrText, 0.9, 0.918).OcrConfidencePercent);
    }

    // ── Ingest-time detection and OCR through search (tasks 2, 8) ───────────────

    [Fact]
    public async Task Extraction_ImageOnlyBook_IsMarkedImageOnly_NotSearchable()
    {
        string scanned = SeedBook("P17SCANNED00000000000001");
        string text = SeedBook("P17TEXTBOOK0000000000001");

        await CreatePipeline(new ScriptedRendererFactory(SearchExtractionQuality.Scanned, pages: 2)).IndexBookAsync(scanned, CancellationToken.None);
        await CreatePipeline(new ScriptedRendererFactory(SearchExtractionQuality.Full, pages: 2)).IndexBookAsync(text, CancellationToken.None);
        _context.ChangeTracker.Clear();

        BookRow scannedRow = _context.Books.Single(book => book.BookId == scanned);
        Assert.Equal((int)SearchBookIndexStatus.Indexed, scannedRow.IndexStatus);
        Assert.Equal((int)BookTextStatus.ImageOnly, scannedRow.TextStatus);
        Assert.Equal(0, scannedRow.TextQuality);
        Assert.Equal((int)BookTextStatus.Searchable, _context.Books.Single(book => book.BookId == text).TextStatus);
        Assert.Equal(1.0, _context.Books.Single(book => book.BookId == text).TextQuality, 3);
    }

    [Fact]
    public async Task Ocr_ThenReindex_KeepsOcrTextSearchable_AndLabelsHits()
    {
        // Fails before: the FtsReindexJob queued after OCR re-ran extraction, hit a duplicate
        // page key (native + OCR rows) and marked the book "Index failed".
        string bookId = SeedBook("P17OCRREINDEX00000000001");
        var renderer = new ScriptedRendererFactory(SearchExtractionQuality.Scanned, pages: 2);
        await CreatePipeline(renderer).IndexBookAsync(bookId, CancellationToken.None);
        _context.ChangeTracker.Clear();

        OcrJobQueueService queue = CreateQueue();
        Assert.True((await queue.QueueBookAsync(bookId)).Queued);
        _context.ChangeTracker.Clear();
        Assert.Equal((int)BookTextStatus.OcrInProgress, _context.Books.AsNoTracking().Single(book => book.BookId == bookId).TextStatus);

        var processor = CreateProcessor(renderer, new PhraseOcrProvider("amber library lantern", 0.93));
        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        _context.ChangeTracker.Clear();

        ExtractionBookResult reindex = await CreatePipeline(renderer).IndexBookAsync(bookId, CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.True(reindex.Succeeded, reindex.ErrorMessage);
        BookRow book = _context.Books.Single(row => row.BookId == bookId);
        Assert.Equal((int)SearchBookIndexStatus.Indexed, book.IndexStatus);
        Assert.Equal((int)BookTextStatus.OcrText, book.TextStatus);
        Assert.True(book.IsOcrDerived);
        Assert.Equal(0.93, book.OcrConfidence!.Value, 3);
        IReadOnlyList<FtsSearchResult> hits = await new FtsIndexService(_context)
            .SearchAsync("amber library lantern", 10, CancellationToken.None);
        FtsSearchResult hit = hits.First(result => result.BookId == bookId);
        Assert.True(hit.IsOcrText);
        Assert.NotNull(hit.PageJumpTarget);
    }

    // ── Failure reporting (task 4) ────────────────────────────────────────────────

    [Fact]
    public async Task OcrFailure_IsTypedPermanentLogged_AndBookShowsOcrFailed()
    {
        // Fails before: every failure was "ocr_processing_failed", retried, and unlogged.
        string bookId = SeedBook("P17OCRFAIL00000000000001");
        var renderer = new ScriptedRendererFactory(SearchExtractionQuality.Scanned, pages: 1);
        await CreatePipeline(renderer).IndexBookAsync(bookId, CancellationToken.None);
        _context.ChangeTracker.Clear();
        Assert.True((await CreateQueue().QueueBookAsync(bookId)).Queued);
        var logger = new CapturingLogger<OcrJobProcessor>();

        var processor = CreateProcessor(
            renderer,
            new ThrowingOcrProvider(new OcrFailureException(OcrFailureCodes.MissingLanguageData)),
            logger);
        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        _context.ChangeTracker.Clear();

        JobRow job = _context.Jobs.Single(row => row.JobType == OcrJobProcessor.JobType && row.BookId == bookId);
        Assert.Equal((int)JobRuntimeStatus.Failed, job.Status);
        Assert.Equal(OcrFailureCodes.MissingLanguageData, job.FailureCode);
        Assert.Equal((int)BookTextStatus.OcrFailed, _context.Books.Single(row => row.BookId == bookId).TextStatus);
        Assert.Contains(logger.Entries, entry => entry.EventName == "ocr.job.failed" && entry.Level == LogLevel.Warning);
        (BookOcrJobState state, string? code) = await BookTextStatusService.LoadOcrJobStateAsync(_context, bookId, CancellationToken.None);
        Assert.Equal(BookOcrJobState.Failed, state);
        Assert.Equal(OcrFailureCodes.MissingLanguageData, code);
    }

    [Fact]
    public void OcrFailure_ExceptionsMapToStableCodes()
    {
        Assert.Equal(OcrFailureCodes.Timeout, OcrJobProcessor.Classify(new TimeoutException()));
        Assert.Equal(OcrFailureCodes.FileUnavailable, OcrJobProcessor.Classify(new FileNotFoundException()));
        Assert.Equal(OcrFailureCodes.UnreadablePage, OcrJobProcessor.Classify(new IOException()));
        Assert.Equal(OcrFailureCodes.UnreadablePage, OcrJobProcessor.Classify(new PdfPasswordRequiredException("locked.pdf")));
        Assert.Equal(OcrFailureCodes.MissingLanguageData, OcrJobProcessor.Classify(new OcrFailureException(OcrFailureCodes.MissingLanguageData)));
        Assert.Equal(OcrFailureCodes.Unexpected, OcrJobProcessor.Classify(new InvalidOperationException()));
        Assert.False(OcrFailureCodes.IsRetryable(OcrFailureCodes.MissingLanguageData));
        Assert.True(OcrFailureCodes.IsRetryable(OcrFailureCodes.Timeout));
        Assert.Equal("Ocr.Failure.ocr_processing_failed", OcrFailureCodes.LocalizationKey("something_else"));
    }

    [Fact]
    public void OcrFailure_EveryCodeAndTextStatus_HasEnglishAndFrenchText()
    {
        var localization = new InMemoryLocalizationService();
        string[] keys =
        [
            .. OcrFailureCodes.All.Select(OcrFailureCodes.LocalizationKey),
            .. Enum.GetValues<BookTextStatus>()
                .Where(status => status != BookTextStatus.OcrText)
                .Select(status => $"Catalogue.TextStatus.{status}"),
            "Catalogue.TextStatus.OcrTextFormat",
            "ActivityCentre.Ocr.MakeSearchableFormat",
            "Search.Result.FromOcrText",
        ];
        foreach (string culture in new[] { "en", "fr" })
        {
            localization.SetCulture(culture);
            foreach (string key in keys)
            {
                Assert.NotEqual(key, localization[key]);
                Assert.DoesNotContain("ocr_", localization[key], StringComparison.Ordinal);
            }
        }
    }

    // ── Automatic policy (task 3) ─────────────────────────────────────────────────

    [Fact]
    public async Task AutoPolicy_QueuesImageOnlyBooks_WhenOnAcPower()
    {
        string scanned = SeedBook("P17AUTOSCAN0000000000001", BookTextStatus.ImageOnly);
        SeedBook("P17AUTOTEXT0000000000001", BookTextStatus.Searchable);
        var queue = new RecordingQueue();

        int queued = await CreatePolicy(queue, new OcrPolicySettings(), onBattery: false).SweepAsync();

        Assert.Equal(1, queued);
        Assert.Equal([scanned], queue.Queued);
        Assert.Equal("eng", queue.Languages.Single());
    }

    [Fact]
    public async Task AutoPolicy_DoesNothing_OnBatteryOrWhenOff()
    {
        SeedBook("P17AUTOBATTERY0000000001", BookTextStatus.ImageOnly);
        var queue = new RecordingQueue();

        Assert.Equal(0, await CreatePolicy(queue, new OcrPolicySettings(), onBattery: true).SweepAsync());
        Assert.Equal(0, await CreatePolicy(queue, new OcrPolicySettings(AutoOcrScannedBooks: false), onBattery: false).SweepAsync());
        Assert.Equal(1, await CreatePolicy(queue, new OcrPolicySettings(PauseOnBattery: false), onBattery: true).SweepAsync());
        Assert.Single(queue.Queued);
    }

    [Fact]
    public async Task AutoPolicy_NeverRequeuesAFailedAttempt_AndIsBounded()
    {
        string failed = SeedBook("P17AUTOFAILED00000000001", BookTextStatus.ImageOnly);
        _context.Jobs.Add(new JobRow
        {
            BookId = failed,
            JobType = OcrJobProcessor.JobType,
            IdempotencyKey = "p17-failed",
            Status = (int)JobRuntimeStatus.Failed,
            FailureCode = OcrFailureCodes.Timeout,
        });
        for (int i = 0; i < OcrAutoPolicyService.MaximumBooksPerSweep + 3; i++)
        {
            SeedBook($"P17AUTOMANY{i:0000000000000}", BookTextStatus.ImageOnly);
        }

        await _context.SaveChangesAsync();
        var queue = new RecordingQueue();

        int queued = await CreatePolicy(queue, new OcrPolicySettings(), onBattery: false).SweepAsync();

        Assert.Equal(OcrAutoPolicyService.MaximumBooksPerSweep, queued);
        Assert.DoesNotContain(failed, queue.Queued);
    }

    [Fact]
    public async Task AutoPolicy_AssessesUnknownStatusesFromExistingPages_BeforeQueueing()
    {
        // Catalogues created before Phase 17 have pages but TextStatus 0 (Unknown).
        string bookId = SeedBook("P17AUTOBACKFILL000000001");
        BookRow row = _context.Books.Single(book => book.BookId == bookId);
        row.IndexStatus = (int)SearchBookIndexStatus.Indexed;
        _context.ExtractedPages.Add(new ExtractedPageRow { BookId = bookId, PageNumber = 0, ExtractionQuality = (int)SearchExtractionQuality.Scanned });
        await _context.SaveChangesAsync();
        var queue = new RecordingQueue();

        Assert.Equal(1, await CreatePolicy(queue, new OcrPolicySettings(), onBattery: false).SweepAsync());
        _context.ChangeTracker.Clear();

        Assert.Equal((int)BookTextStatus.ImageOnly, _context.Books.Single(book => book.BookId == bookId).TextStatus);
    }

    [Fact]
    public async Task OcrWorker_WhenIdle_SweepsAtMostOncePerInterval()
    {
        var policy = new CountingPolicy();
        var time = new ManualTime(DateTimeOffset.Parse("2026-09-25T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var worker = new OcrWorker(new IdleProcessor(), autoPolicy: policy, time: time);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);
        time.Advance(OcrWorker.SweepInterval);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, policy.Sweeps);
    }

    // ── Queue: suggestion and bulk action ─────────────────────────────────────────

    [Fact]
    public async Task Queue_CountsAndQueuesBooksNeedingOcr_AndNeverDuplicatesPausedJobs()
    {
        string root = _root;
        {
            string a = SeedBookWithFile("P17BULKA0000000000000001", root, BookTextStatus.ImageOnly);
            string b = SeedBookWithFile("P17BULKB0000000000000001", root, BookTextStatus.PartlySearchable);
            string paused = SeedBookWithFile("P17BULKP0000000000000001", root, BookTextStatus.ImageOnly);
            SeedBookWithFile("P17BULKT0000000000000001", root, BookTextStatus.Searchable);
            _context.Jobs.Add(new JobRow
            {
                BookId = paused,
                JobType = OcrJobProcessor.JobType,
                IdempotencyKey = "p17-paused",
                Status = (int)JobRuntimeStatus.Paused,
            });
            await _context.SaveChangesAsync();
            var queue = new OcrJobQueueService(_context, root);

            Assert.Equal(2, await queue.CountBooksNeedingOcrAsync());
            OcrQueueResult pausedResult = await queue.QueueBookAsync(paused);
            Assert.Equal(2, await queue.QueueBooksNeedingOcrAsync());
            _context.ChangeTracker.Clear();

            Assert.True(pausedResult.AlreadyQueued);
            Assert.Equal(1, _context.Jobs.Count(job => job.BookId == paused));
            Assert.Equal(0, await queue.CountBooksNeedingOcrAsync());
            Assert.All(new[] { a, b }, id => Assert.Equal(
                (int)BookTextStatus.OcrInProgress,
                _context.Books.Single(book => book.BookId == id).TextStatus));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static BookPageTextEvidence[] Pages(int count, SearchExtractionQuality quality, int words, int start = 0) =>
        [.. Enumerable.Range(start, count).Select(index => new BookPageTextEvidence(index, quality, words))];

    private string SeedBook(string bookId, BookTextStatus status = BookTextStatus.Unknown)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Phase 17 Book",
            Sha256Hash = new string('b', 64),
            Status = 0,
            TextStatus = (int)status,
        });
        _context.SaveChanges();
        return bookId;
    }

    private string SeedBookWithFile(string bookId, string root, BookTextStatus status)
    {
        string relative = $"{bookId}.pdf";
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, relative), "%PDF-1.7");
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Phase 17 Book",
            RelativePath = relative,
            Status = 0,
            TextStatus = (int)status,
            BookFiles = [new BookFileRow { BookId = bookId, RelativePath = relative, FileStatus = 0, LastSeenUtc = DateTimeOffset.UtcNow }],
        });
        _context.SaveChanges();
        return bookId;
    }

    private ExtractionPipelineService CreatePipeline(IPdfRendererFactory renderer) =>
        new(
            _context,
            new FixedLocator(),
            renderer,
            new ExtractedTextStore(_context),
            new SearchChunkRepository(_context),
            new SearchChunker());

    private OcrJobQueueService CreateQueue()
    {
        // The queue resolves a real file; point the book at one.
        string root = _root;
        Directory.CreateDirectory(root);
        foreach (BookRow book in _context.Books.Include(row => row.BookFiles).ToList())
        {
            if (book.BookFiles.Count == 0)
            {
                string relative = $"{book.BookId}.pdf";
                File.WriteAllText(Path.Combine(root, relative), "%PDF-1.7");
                book.BookFiles.Add(new BookFileRow { BookId = book.BookId, RelativePath = relative, FileStatus = 0, LastSeenUtc = DateTimeOffset.UtcNow });
            }
        }

        _context.SaveChanges();
        return new OcrJobQueueService(_context, root);
    }

    private OcrJobProcessor CreateProcessor(IPdfRendererFactory renderer, IOcrProvider provider, ILogger<OcrJobProcessor>? logger = null)
    {
        var factory = new FileContextFactory(_dbPath);
        return new OcrJobProcessor(
            factory,
            renderer,
            provider,
            new ExtractedTextStore(factory),
            new SearchChunkRepository(factory),
            new SearchChunker(),
            logger: logger);
    }

    private OcrAutoPolicyService CreatePolicy(RecordingQueue queue, OcrPolicySettings settings, bool onBattery) =>
        new(new FileContextFactory(_dbPath), queue, new FixedSettings(settings), new FixedPower(onBattery));

    private sealed class FileContextFactory(string dbPath) : IDbContextFactory<CatalogueDbContext>
    {
        private readonly DbContextOptions<CatalogueDbContext> _options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False", sqlite => sqlite.MigrationsAssembly("OgmaLibrary.Infrastructure"))
            .Options;

        public CatalogueDbContext CreateDbContext() => new(_options);
    }

    private sealed class FixedLocator : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) =>
            Task.FromResult<string?>($"C:\\fake\\{bookId}.pdf");
    }

    private sealed class ScriptedRendererFactory(SearchExtractionQuality quality, int pages) : IPdfRendererFactory
    {
        public IPdfRenderer Open(string filePath) => new Renderer(quality, pages);

        private sealed class Renderer(SearchExtractionQuality quality, int pages) : IPdfRenderer
        {
            public int PageCount => pages;

            public void Dispose()
            {
            }

            public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
                Task.FromResult(new RenderResult([(byte)pageIndex], 612, 792, pageIndex));

            public int GetPageRotationDegrees(int pageIndex) => 0;

            public TextLayer ExtractTextLayer(int pageIndex) => quality == SearchExtractionQuality.Full
                ? new TextLayer(
                    pageIndex,
                    [.. Enumerable.Range(0, 40).Select(i => new TextWord($"word{i}", 0, 0, 0.1, 0.1))],
                    ExtractionQuality.Full)
                : new TextLayer(pageIndex, [], (ExtractionQuality)(int)quality);
        }
    }

    private sealed class PhraseOcrProvider(string phrase, double confidence) : IOcrProvider
    {
        public Task<OcrPageResult> RecognizeAsync(Stream pageImage, string languageHint, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OcrPageResult($"{phrase} on a scanned page", confidence));
    }

    private sealed class ThrowingOcrProvider(Exception error) : IOcrProvider
    {
        public Task<OcrPageResult> RecognizeAsync(Stream pageImage, string languageHint, CancellationToken cancellationToken = default) =>
            Task.FromException<OcrPageResult>(error);
    }

    private sealed class RecordingQueue : IOcrJobQueueService
    {
        public List<string> Queued { get; } = [];

        public List<string> Languages { get; } = [];

        public Task<OcrQueueResult> QueueBookAsync(string bookId, string languageHint = "eng", CancellationToken cancellationToken = default)
        {
            Queued.Add(bookId);
            Languages.Add(languageHint);
            return Task.FromResult(new OcrQueueResult(true, false, Queued.Count, null));
        }
    }

    private sealed class FixedSettings(OcrPolicySettings settings) : IOcrPolicySettingsStore
    {
        public Task<OcrPolicySettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(OcrPolicySettings value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedPower(bool onBattery) : IPowerSource
    {
        public bool IsOnBattery => onBattery;
    }

    private sealed class CountingPolicy : IOcrAutoPolicy
    {
        public int Sweeps { get; private set; }

        public Task<int> SweepAsync(CancellationToken cancellationToken = default)
        {
            Sweeps++;
            return Task.FromResult(0);
        }
    }

    private sealed class IdleProcessor : IOcrJobProcessor
    {
        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string? EventName)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId.Name));
    }
}
