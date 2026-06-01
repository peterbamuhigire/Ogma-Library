using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Tests.GoldenCorpus;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Phase 15 OCR golden-corpus acceptance tests.</summary>
public sealed class OcrGoldenCorpusTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public OcrGoldenCorpusTests()
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
    public async Task OcrJob_ScannedPdf_BecomesSearchable()
    {
        string corpusRoot = FindCorpusRoot("ocr-pipeline");
        Assert.True(ManifestVerifier.AllMatch(corpusRoot, Path.Combine(corpusRoot, "MANIFEST.sha256")));
        string fixturePath = Path.Combine(corpusRoot, "scanned-image-only.pdf");
        string[] expectedWords = File.ReadAllLines(Path.Combine(corpusRoot, "expected-words.txt"))
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();
        string bookId = SeedBook("OCRGOLDENSCANNED001");
        SeedOcrJob(bookId, 0, new OcrJobPayload(fixturePath));
        var processor = CreateProcessor(pageCount: 10, pageTextFactory: page =>
            $"Page {page} rareocrkeyword classroom library searchable");

        bool processed = await processor.ProcessNextAsync(CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.True(processed);
        Assert.True(_context.Books.Single(book => book.BookId == bookId).IsOcrDerived);
        Assert.Equal(10, _context.ExtractedPages.Count(page => page.BookId == bookId && page.Source == "OCR"));
        Assert.Equal(10, _context.SearchChunks.Count(chunk => chunk.BookId == bookId));

        var fts = new FtsIndexService(_context);
        foreach (string word in expectedWords)
        {
            IReadOnlyList<FtsSearchResult> results = await fts.SearchAsync(word, 10, CancellationToken.None);
            Assert.Contains(results, result => result.BookId == bookId && result.Source == SearchChunkSource.Page);
        }
    }

    [Fact]
    public async Task OcrJob_VeryLargePdf_NoOutOfMemory()
    {
        const int pageCount = 1_000;
        string bookId = SeedBook("OCRLARGEPROFILE0001");
        SeedOcrJob(bookId, 0, new OcrJobPayload("large-synthetic.pdf"));
        var processor = CreateProcessor(pageCount, page => $"large synthetic OCR page {page} searchable");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long beforeBytes = GC.GetTotalMemory(forceFullCollection: true);

        bool processed = await processor.ProcessNextAsync(CancellationToken.None);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long afterBytes = GC.GetTotalMemory(forceFullCollection: true);

        Assert.True(processed);
        Assert.Equal(pageCount, _context.ExtractedPages.Count(page => page.BookId == bookId && page.Source == "OCR"));
        Assert.InRange(afterBytes - beforeBytes, long.MinValue, 512L * 1024L * 1024L);
    }

    private OcrJobProcessor CreateProcessor(int pageCount, Func<int, string> pageTextFactory)
    {
        var factory = new TestContextFactory(_dbPath);
        return new OcrJobProcessor(
            factory,
            new FakePdfRendererFactory(pageCount),
            new OracleOcrProvider(pageTextFactory),
            new ExtractedTextStore(factory),
            new SearchChunkRepository(factory),
            new SearchChunker());
    }

    private string SeedBook(string bookId)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "OCR Golden Corpus",
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
        });
        _context.SaveChanges();
    }

    private static string FindCorpusRoot(string corpusName)
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "tests", "golden-corpus", corpusName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find tests/golden-corpus/{corpusName}.");
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

    private sealed class FakePdfRendererFactory(int pageCount) : IPdfRendererFactory
    {
        public IPdfRenderer Open(string filePath) => new FakePdfRenderer(pageCount);
    }

    private sealed class FakePdfRenderer(int pageCount) : IPdfRenderer
    {
        public int PageCount => pageCount;

        public void Dispose()
        {
        }

        public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
            Task.FromResult(new RenderResult([(byte)(pageIndex % 251)], 612, 792, pageIndex));

        public int GetPageRotationDegrees(int pageIndex) => 0;

        public TextLayer ExtractTextLayer(int pageIndex) =>
            new(pageIndex, [], ExtractionQuality.Scanned);
    }

    private sealed class OracleOcrProvider(Func<int, string> pageTextFactory) : IOcrProvider
    {
        public Task<OcrPageResult> RecognizeAsync(
            Stream pageImage,
            string languageHint,
            CancellationToken cancellationToken = default)
        {
            int page = pageImage.ReadByte();
            return Task.FromResult(new OcrPageResult(pageTextFactory(page), 0.98));
        }
    }
}
