using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Phase 15 OCR job processor reliability tests.</summary>
public sealed class OcrJobProcessorTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public OcrJobProcessorTests()
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
    public async Task OcrJobProcessor_ProcessesPendingJob_MarksBookOcrDerived()
    {
        string bookId = SeedBook("BOOKOCRPROCESS000000001");
        SeedOcrJob(bookId, 0, new OcrJobPayload("sample.pdf"));
        var provider = new FakeOcrProvider();
        var processor = CreateProcessor(new FakePdfRendererFactory(pageCount: 3), provider);

        bool processed = await processor.ProcessNextAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.True(processed);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(3, _context.ExtractedPages.Count(page => page.BookId == bookId && page.Source == "OCR"));
        Assert.True(_context.Books.Single(book => book.BookId == bookId).IsOcrDerived);
        Assert.Equal(3, _context.SearchChunks.Count(chunk => chunk.BookId == bookId));
        Assert.Contains(await new FtsIndexService(_context).SearchAsync("recognized", 10, CancellationToken.None),
            result => result.BookId == bookId && result.Source == SearchChunkSource.Page);
        Assert.Contains(_context.Jobs, job => job.BookId == bookId && job.JobType == "FtsReindexJob");
        Assert.Contains(_context.Jobs, job => job.BookId == bookId && job.JobType == "EmbeddingJob");
        Assert.Equal(2, _context.Jobs.Single(job => job.JobType == OcrJobProcessor.JobType).Status);
    }

    [Fact]
    public async Task OcrJobProcessor_TextPages_AreSkippedWithoutInvokingOcr()
    {
        string bookId = SeedBook("BOOKOCRTEXTSKIP00000001");
        SeedOcrJob(bookId, 0, new OcrJobPayload("sample.pdf"));
        var provider = new FakeOcrProvider();
        var processor = CreateProcessor(
            new FakePdfRendererFactory(pageCount: 3, ExtractionQuality.Full),
            provider);

        bool processed = await processor.ProcessNextAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.True(processed);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(_context.ExtractedPages.Where(page => page.BookId == bookId && page.Source == "OCR"));
        Assert.False(_context.Books.Single(book => book.BookId == bookId).IsOcrDerived);
    }

    [Fact]
    public async Task OcrJobProcessor_GoodPrimaryText_RemainsSelectedOverOcrAlternative()
    {
        string bookId = SeedBook("BOOKOCRPRIMARY00000001");
        _context.ExtractedPages.Add(new ExtractedPageRow
        {
            BookId = bookId,
            PageNumber = 0,
            TextContent = "trusted primary text",
            ExtractionQuality = (int)SearchExtractionQuality.Full,
            WordCount = 3,
            Source = "Extraction",
            IsSelectedText = true,
            ExtractionUtc = DateTimeOffset.UtcNow,
        });
        _context.SaveChanges();
        SeedOcrJob(bookId, 0, new OcrJobPayload("sample.pdf"));
        var processor = CreateProcessor(new FakePdfRendererFactory(pageCount: 1), new FakeOcrProvider());

        await processor.ProcessNextAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        ExtractedPageRow primary = _context.ExtractedPages.Single(page =>
            page.BookId == bookId && page.Source == "Extraction");
        ExtractedPageRow ocr = _context.ExtractedPages.Single(page =>
            page.BookId == bookId && page.Source == "OCR");
        SearchChunkRow chunk = _context.SearchChunks.Single(row => row.BookId == bookId);
        Assert.Equal((int)SearchExtractionQuality.Full, primary.ExtractionQuality);
        Assert.Equal(3, primary.WordCount);
        Assert.False(OcrPageQualityPolicy.ShouldSelectOcr(
            SearchExtractionQuality.Full,
            primary.WordCount,
            ocr.TextContent,
            ocr.OcrConfidence ?? 0));
        Assert.True(primary.IsSelectedText);
        Assert.False(ocr.IsSelectedText);
        Assert.Contains("trusted primary text", chunk.ChunkText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OcrJob_Recovery_AfterInterruption_NoDuplicatePages()
    {
        string bookId = SeedBook("BOOKOCRRECOVER00000001");
        SeedOcrJob(bookId, 1, new OcrJobPayload("sample.pdf", TotalPages: 3, ProcessedPages: 1));
        _context.ExtractedPages.Add(new ExtractedPageRow
        {
            BookId = bookId,
            PageNumber = 0,
            TextContent = "already processed",
            Source = "OCR",
            ExtractionQuality = 0,
            WordCount = 2,
            ExtractionUtc = DateTimeOffset.UtcNow,
        });
        _context.SaveChanges();
        var provider = new FakeOcrProvider();
        var processor = CreateProcessor(new FakePdfRendererFactory(pageCount: 3), provider);

        await processor.ProcessNextAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.Equal(2, provider.CallCount);
        Assert.Equal(3, _context.ExtractedPages.Count(page => page.BookId == bookId && page.Source == "OCR"));
        JobRow job = _context.Jobs.Single(job => job.JobType == OcrJobProcessor.JobType);
        Assert.Equal(2, job.Status);
        Assert.Equal(1, job.RetryCount);
    }

    private OcrJobProcessor CreateProcessor(FakePdfRendererFactory rendererFactory, FakeOcrProvider provider)
    {
        var factory = new TestContextFactory(_dbPath);
        return new OcrJobProcessor(
            factory,
            rendererFactory,
            provider,
            new ExtractedTextStore(factory),
            new SearchChunkRepository(factory),
            new SearchChunker());
    }

    private string SeedBook(string bookId)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "OCR Test Book",
            Status = 0,
        });
        _context.SaveChanges();
        return bookId;
    }

    private void SeedOcrJob(string bookId, int status, OcrJobPayload payload)
    {
        _context.Jobs.Add(new JobRow
        {
            BookId = bookId,
            JobType = OcrJobProcessor.JobType,
            IdempotencyKey = $"{bookId}-ocr",
            Status = status,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            StartedUtc = status == 1 ? DateTimeOffset.UtcNow.AddMinutes(-5) : null,
        });
        _context.SaveChanges();
    }

    private sealed class TestContextFactory : IDbContextFactory<CatalogueDbContext>
    {
        private readonly DbContextOptions<CatalogueDbContext> _options;

        public TestContextFactory(string dbPath)
        {
            _options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False",
                    sqlite => sqlite.MigrationsAssembly("OgmaLibrary.Infrastructure"))
                .Options;
        }

        public CatalogueDbContext CreateDbContext() => new(_options);
    }

    private sealed class FakePdfRendererFactory(
        int pageCount,
        ExtractionQuality quality = ExtractionQuality.Scanned) : IPdfRendererFactory
    {
        public IPdfRenderer Open(string filePath) => new FakePdfRenderer(pageCount, quality);
    }

    private sealed class FakePdfRenderer(int pageCount, ExtractionQuality quality) : IPdfRenderer
    {
        public int PageCount => pageCount;

        public void Dispose()
        {
        }

        public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
            Task.FromResult(new RenderResult([(byte)pageIndex], 612, 792, pageIndex));

        public int GetPageRotationDegrees(int pageIndex) => 0;

        public TextLayer ExtractTextLayer(int pageIndex) =>
            new(pageIndex, [], quality);
    }

    private sealed class FakeOcrProvider : IOcrProvider
    {
        public int CallCount { get; private set; }

        public async Task<OcrPageResult> RecognizeAsync(
            Stream pageImage,
            string languageHint,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            int page = pageImage.ReadByte();
            await Task.CompletedTask;
            return new OcrPageResult($"recognized page {page}", 0.98);
        }
    }
}
