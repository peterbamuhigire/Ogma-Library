using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Phase 15 OCR queue trigger integration tests.</summary>
public sealed class OcrJobQueueServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly string _libraryRoot;

    public OcrJobQueueServiceTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _libraryRoot = Path.Combine(Path.GetTempPath(), $"ogma-ocr-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_libraryRoot);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
        if (Directory.Exists(_libraryRoot))
        {
            Directory.Delete(_libraryRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueBookAsync_CreatesOcrJob_WithResolvedFilePayload()
    {
        string bookId = SeedBookWithFile("OCRQUEUEBOOK000000001", "books/scan.pdf");
        string expectedPath = CreateLibraryFile("books/scan.pdf");
        var service = new OcrJobQueueService(_context, _libraryRoot);

        var result = await service.QueueBookAsync(bookId);
        _context.ChangeTracker.Clear();

        Assert.True(result.Queued);
        Assert.False(result.AlreadyQueued);
        JobRow job = await _context.Jobs.SingleAsync(job => job.BookId == bookId && job.JobType == "OcrJob");
        Assert.Equal(0, job.Status);
        Assert.Equal(result.JobId, job.JobId);
        using JsonDocument payload = JsonDocument.Parse(job.Payload!);
        Assert.Equal(expectedPath, payload.RootElement.GetProperty("FilePath").GetString());
        Assert.Equal("eng", payload.RootElement.GetProperty("Language").GetString());
    }

    [Fact]
    public async Task QueueBookAsync_ExistingPendingJob_IsIdempotent()
    {
        string bookId = SeedBookWithFile("OCRQUEUEIDEMPOTENT001", "books/scan.pdf");
        CreateLibraryFile("books/scan.pdf");
        var service = new OcrJobQueueService(_context, _libraryRoot);

        var first = await service.QueueBookAsync(bookId);
        var second = await service.QueueBookAsync(bookId);

        Assert.True(first.Queued);
        Assert.False(second.Queued);
        Assert.True(second.AlreadyQueued);
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal(1, await _context.Jobs.CountAsync(job => job.BookId == bookId && job.JobType == "OcrJob"));
    }

    [Fact]
    public async Task QueueBookAsync_FailedJob_IsRetriedInsteadOfDuplicated()
    {
        string bookId = SeedBookWithFile("OCRQUEUERETRY000001", "books/scan.pdf");
        CreateLibraryFile("books/scan.pdf");
        _context.Jobs.Add(new JobRow
        {
            BookId = bookId,
            JobType = "OcrJob",
            IdempotencyKey = "failed-ocr-job",
            Status = 3,
            Payload = "{}",
            ErrorMessage = "OCR failed",
            CompletedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await _context.SaveChangesAsync();
        var service = new OcrJobQueueService(_context, _libraryRoot);

        var result = await service.QueueBookAsync(bookId, languageHint: "fra");
        _context.ChangeTracker.Clear();

        Assert.True(result.Queued);
        JobRow job = await _context.Jobs.SingleAsync(job => job.BookId == bookId && job.JobType == "OcrJob");
        Assert.Equal(0, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Null(job.ErrorMessage);
        Assert.Null(job.CompletedUtc);
        using JsonDocument payload = JsonDocument.Parse(job.Payload!);
        Assert.Equal("fra", payload.RootElement.GetProperty("Language").GetString());
    }

    [Fact]
    public async Task QueueBookAsync_MissingFile_ReturnsUserVisibleError()
    {
        string bookId = SeedBookWithFile("OCRQUEUEMISSING0001", "books/missing.pdf");
        var service = new OcrJobQueueService(_context, _libraryRoot);

        var result = await service.QueueBookAsync(bookId);

        Assert.False(result.Queued);
        Assert.False(result.AlreadyQueued);
        Assert.Equal("No available PDF file was found for OCR.", result.ErrorMessage);
        Assert.Empty(_context.Jobs.Where(job => job.BookId == bookId && job.JobType == "OcrJob"));
    }

    [Fact]
    public async Task QueueBookAsync_PathTraversalOutsideLibraryRoot_IsRejected()
    {
        string bookId = SeedBookWithFile("OCRQUEUETRAVERSAL01", "../outside.pdf");
        var service = new OcrJobQueueService(_context, _libraryRoot);

        var result = await service.QueueBookAsync(bookId);

        Assert.False(result.Queued);
        Assert.Equal("No available PDF file was found for OCR.", result.ErrorMessage);
        Assert.Empty(_context.Jobs.Where(job => job.BookId == bookId && job.JobType == "OcrJob"));
    }

    private string SeedBookWithFile(string bookId, string relativePath)
    {
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Scanned Book",
            RelativePath = relativePath,
            Status = 0,
            BookFiles =
            [
                new BookFileRow
                {
                    BookId = bookId,
                    RelativePath = relativePath,
                    FileStatus = 0,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                },
            ],
        });
        _context.SaveChanges();
        return bookId;
    }

    private string CreateLibraryFile(string relativePath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_libraryRoot, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "%PDF-1.7");
        return fullPath;
    }
}
