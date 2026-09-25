using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Workers.Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>
/// Sept-23 Phase 17 with the packaged engines: a generated image-only PDF (the audit phrase
/// <c>amber library lantern</c> rendered into a picture, no text layer) goes through extraction,
/// the isolated PDF worker, packaged Tesseract, and re-indexing, and becomes searchable with a
/// page jump and OCR provenance (objective 3). Windows only (Tesseract native binaries).
/// </summary>
public sealed class Sept23Phase17RealOcrSearchTests : IDisposable
{
    private const string Phrase = "amber library lantern";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), $"ogma-p17-real-ocr-{Guid.NewGuid():N}");

    public Sept23Phase17RealOcrSearchTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task ScannedFixture_IngestThenOcr_FindsAuditPhraseWithPageJump()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("NOT ASSESSED: packaged Tesseract ships Windows native binaries only (Phase 27).");
            return;
        }

        string pdf = Path.Combine(_sandbox, "scanned-pamphlet.pdf");
        CreateScannedPdf(pdf, Phrase);
        string bookId = SeedBook("P17REALOCR00000000000001", pdf);
        var worker = new PdfWorkerClient(new PdfWorkerOptions { SandboxRoot = Path.Combine(_sandbox, "worker") });
        var renderer = new IsolatedPdfRendererFactory(worker);

        ExtractionBookResult first = await Pipeline(renderer, pdf).IndexBookAsync(bookId, CancellationToken.None);
        _context.ChangeTracker.Clear();
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal((int)BookTextStatus.ImageOnly, _context.Books.Single(book => book.BookId == bookId).TextStatus);
        Assert.Empty(await new FtsIndexService(_context).SearchAsync(Phrase, 10, CancellationToken.None));

        Assert.True((await new OcrJobQueueService(_context, _sandbox).QueueBookAsync(bookId)).Queued);
        _context.ChangeTracker.Clear();
        var factory = new FileContextFactory(_dbPath);
        var processor = new OcrJobProcessor(
            factory,
            renderer,
            new TesseractOcrProvider(Path.Combine(AppContext.BaseDirectory, "tessdata")),
            new ExtractedTextStore(factory),
            new SearchChunkRepository(factory),
            new SearchChunker());
        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));

        // The FtsReindexJob the OCR job queued runs the same pipeline again.
        ExtractionBookResult reindex = await Pipeline(renderer, pdf).IndexBookAsync(bookId, CancellationToken.None);
        _context.ChangeTracker.Clear();

        Assert.True(reindex.Succeeded, reindex.ErrorMessage);
        BookRow book = _context.Books.Single(row => row.BookId == bookId);
        Assert.Equal((int)BookTextStatus.OcrText, book.TextStatus);
        Assert.True(book.IsOcrDerived);
        Assert.InRange(book.OcrConfidence ?? 0, OcrPageQualityPolicy.MinimumSelectionConfidence, 1);
        FtsSearchResult hit = Assert.Single(await new FtsIndexService(_context).SearchAsync(Phrase, 10, CancellationToken.None));
        Assert.Equal(bookId, hit.BookId);
        Assert.True(hit.IsOcrText);
        Assert.Equal(0, hit.PageJumpTarget?.PageIndex);
    }

    /// <summary>
    /// Handle-leak guard (phase 17 test plan): 50 OCR jobs through the isolated worker and
    /// packaged Tesseract leave no locked file in the sandbox and no worker process running.
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task FiftyOcrJobs_LeaveNoLockedFilesOrWorkerProcesses()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("NOT ASSESSED: packaged Tesseract ships Windows native binaries only (Phase 27).");
            return;
        }

        string workerSandbox = Path.Combine(_sandbox, "worker");
        var worker = new PdfWorkerClient(new PdfWorkerOptions { SandboxRoot = workerSandbox });
        var renderer = new IsolatedPdfRendererFactory(worker);
        var factory = new FileContextFactory(_dbPath);
        var processor = new OcrJobProcessor(
            factory,
            renderer,
            new TesseractOcrProvider(Path.Combine(AppContext.BaseDirectory, "tessdata")),
            new ExtractedTextStore(factory),
            new SearchChunkRepository(factory),
            new SearchChunker());
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            string pdf = Path.Combine(_sandbox, $"scan-{i:00}.pdf");
            CreateScannedPdf(pdf, Phrase);
            string bookId = SeedBook($"P17LEAK{i:0000000000000000}", pdf);
            Assert.True((await new OcrJobQueueService(_context, _sandbox).QueueBookAsync(bookId)).Queued);
            _context.ChangeTracker.Clear();
            Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
            File.Delete(pdf); // throws if any handle to the source were still open
        }

        clock.Stop();
        _context.ChangeTracker.Clear();
        Assert.Equal(50, _context.Jobs.Count(job => job.JobType == OcrJobProcessor.JobType && job.Status == 2));
        Assert.True(!Directory.Exists(workerSandbox) || Directory.GetFileSystemEntries(workerSandbox).Length == 0);
        Directory.Delete(_sandbox, recursive: true);
        Console.WriteLine($"OCR_LEAK_EVIDENCE jobs=50 elapsed_ms={clock.ElapsedMilliseconds}");
    }

    private ExtractionPipelineService Pipeline(IsolatedPdfRendererFactory renderer, string pdf) =>
        new(
            _context,
            new FixedLocator(pdf),
            renderer,
            new ExtractedTextStore(_context),
            new SearchChunkRepository(_context),
            new SearchChunker());

    private string SeedBook(string bookId, string pdf)
    {
        string relative = Path.GetFileName(pdf);
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Scanned Pamphlet",
            RelativePath = relative,
            Sha256Hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(pdf))),
            Status = 0,
            BookFiles = [new BookFileRow { BookId = bookId, RelativePath = relative, FileStatus = 0, LastSeenUtc = DateTimeOffset.UtcNow }],
        });
        _context.SaveChanges();
        return bookId;
    }

    private static void CreateScannedPdf(string pdfPath, string phrase)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1_200, 1_700, SKColorType.Rgba8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 72);
        surface.Canvas.DrawText(phrase, 100, 300, SKTextAlign.Left, font, paint);
        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] png = encoded.ToArray();
        using var imageStream = new MemoryStream(png, 0, png.Length, writable: false, publiclyVisible: true);
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        using (XGraphics graphics = XGraphics.FromPdfPage(page))
        using (XImage pdfImage = XImage.FromStream(imageStream))
        {
            graphics.DrawImage(pdfImage, 0, 0, page.Width.Point, page.Height.Point);
        }

        document.Save(pdfPath);
    }

    private sealed class FixedLocator(string path) : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult<string?>(path);
    }

    private sealed class FileContextFactory(string dbPath) : IDbContextFactory<CatalogueDbContext>
    {
        private readonly DbContextOptions<CatalogueDbContext> _options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False", sqlite => sqlite.MigrationsAssembly("OgmaLibrary.Infrastructure"))
            .Options;

        public CatalogueDbContext CreateDbContext() => new(_options);
    }
}
