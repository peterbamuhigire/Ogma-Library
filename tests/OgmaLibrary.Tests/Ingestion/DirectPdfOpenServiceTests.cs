using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Tests for directly opening a single PDF without scanning a folder.</summary>
public sealed class DirectPdfOpenServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public DirectPdfOpenServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ogma-direct-open-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task DirectPdfOpen_RegistersSelectedPdf_AndSetsContainingFolderAsRoot()
    {
        string pdfPath = Path.Combine(_tempRoot, "single.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-1.4\n% direct open test\n"u8.ToArray());

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        var settings = new LibrarySettingsService(_tempRoot);
        var identity = new BookIdentityService(context);
        var registration = new BookRegistrationService(context);
        var service = new DirectPdfOpenService(settings, identity, registration);

        string bookId = await service.OpenAsync(pdfPath);

        Assert.False(string.IsNullOrWhiteSpace(bookId));
        Assert.Equal(_tempRoot, await settings.GetLibraryRootAsync());

        var book = context.Books.Single(b => b.BookId == bookId);
        Assert.Equal(0, book.Status);
        Assert.NotNull(book.Sha256Hash);

        var file = context.BookFiles.Single(f => f.BookId == bookId);
        Assert.Equal("single.pdf", file.RelativePath);
        Assert.Equal(0, file.FileStatus);
    }

    [Fact]
    public async Task DirectPdfOpen_ExternalPdf_AddsBookWithoutChangingExistingLibraryRoot()
    {
        string libraryRoot = Path.Combine(_tempRoot, "library");
        string externalRoot = Path.Combine(_tempRoot, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        string pdfPath = Path.Combine(externalRoot, "outside.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-1.4\n% external direct open test\n"u8.ToArray());

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context));

        string bookId = await service.OpenAsync(pdfPath);

        Assert.Equal(libraryRoot, await settings.GetLibraryRootAsync());

        var file = context.BookFiles.Single(f => f.BookId == bookId);
        Assert.Equal(
            pdfPath.Replace(Path.DirectorySeparatorChar, '/'),
            file.RelativePath);

        var locator = new BookFileLocator(context, settings);
        Assert.Equal(pdfPath, await locator.LocateAsync(bookId, CancellationToken.None));
    }

    [Fact]
    public async Task DirectPdfOpen_ExistingMatch_QueuesMetadataAndThumbnailJobs()
    {
        string libraryRoot = Path.Combine(_tempRoot, "library");
        string externalRoot = Path.Combine(_tempRoot, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        string pdfPath = Path.Combine(externalRoot, "existing.pdf");
        byte[] content = "%PDF-1.4\n% existing direct open test\n"u8.ToArray();
        await File.WriteAllBytesAsync(pdfPath, content);
        string sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "EXISTING01",
            Status = 1,
            Sha256Hash = sha256,
        });
        await context.SaveChangesAsync();

        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context));

        string bookId = await service.OpenAsync(pdfPath);

        Assert.Equal("EXISTING01", bookId);
        Assert.Equal(0, context.Books.Single(b => b.BookId == bookId).Status);
        Assert.Contains(context.Jobs, j => j.BookId == bookId && j.JobType == "MetadataExtraction" && j.Payload == pdfPath);
        Assert.Contains(context.Jobs, j => j.BookId == bookId && j.JobType == "ThumbnailGeneration" && j.Payload == pdfPath);
    }

    [Fact]
    public async Task DirectPdfOpen_ExistingMatchWithPriorJobs_QueuesJobsForSelectedFileVersion()
    {
        string libraryRoot = Path.Combine(_tempRoot, "library");
        string externalRoot = Path.Combine(_tempRoot, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        string pdfPath = Path.Combine(externalRoot, "changed.pdf");
        byte[] oldContent = "%PDF-1.4\n% old content\n"u8.ToArray();
        byte[] selectedContent = "%PDF-1.4\n% selected changed content\n"u8.ToArray();
        await File.WriteAllBytesAsync(pdfPath, selectedContent);

        string oldHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(oldContent));
        string selectedHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(selectedContent));
        string storedPath = pdfPath.Replace(Path.DirectorySeparatorChar, '/');

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "EXISTING02",
            Status = 0,
            Sha256Hash = oldHash,
        });
        context.BookFiles.Add(new BookFileRow
        {
            BookId = "EXISTING02",
            RelativePath = storedPath,
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        context.Jobs.Add(new JobRow
        {
            BookId = "EXISTING02",
            JobType = "MetadataExtraction",
            IdempotencyKey = ComputeJobKey("EXISTING02", "MetadataExtraction", oldHash),
            Status = 2,
            Payload = @"C:\old\changed.pdf",
        });
        context.Jobs.Add(new JobRow
        {
            BookId = "EXISTING02",
            JobType = "ThumbnailGeneration",
            IdempotencyKey = ComputeJobKey("EXISTING02", "ThumbnailGeneration", oldHash),
            Status = 2,
            Payload = @"C:\old\changed.pdf",
        });
        await context.SaveChangesAsync();

        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context));

        string bookId = await service.OpenAsync(pdfPath);

        Assert.Equal("EXISTING02", bookId);
        Assert.Equal(selectedHash, context.Books.Single(b => b.BookId == bookId).Sha256Hash);
        Assert.Contains(context.Jobs, j =>
            j.BookId == bookId &&
            j.JobType == "MetadataExtraction" &&
            j.Status == 0 &&
            j.IdempotencyKey == ComputeJobKey(bookId, "MetadataExtraction", selectedHash) &&
            j.Payload == pdfPath);
        Assert.Contains(context.Jobs, j =>
            j.BookId == bookId &&
            j.JobType == "ThumbnailGeneration" &&
            j.Status == 0 &&
            j.IdempotencyKey == ComputeJobKey(bookId, "ThumbnailGeneration", selectedHash) &&
            j.Payload == pdfPath);
    }

    [Fact]
    public async Task DirectPdfOpen_RejectsNonPdfFile()
    {
        string textPath = Path.Combine(_tempRoot, "notes.txt");
        await File.WriteAllTextAsync(textPath, "not a pdf");

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        var settings = new LibrarySettingsService(_tempRoot);
        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenAsync(textPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (Exception)
        {
            // Best effort cleanup for Windows file-handle timing.
        }
    }

    private static string ComputeJobKey(string bookId, string jobType, string discriminator)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"{bookId}|{jobType}|{discriminator}");
        byte[] hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
