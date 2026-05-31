using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
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
        var service = new DirectPdfOpenService(settings, identity, registration, context: context);

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
            new BookRegistrationService(context),
            context: context);

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
        context.BookFiles.Add(new BookFileRow
        {
            BookId = "EXISTING01",
            RelativePath = pdfPath.Replace(Path.DirectorySeparatorChar, '/'),
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await context.SaveChangesAsync();

        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context),
            context: context);

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
            new BookRegistrationService(context),
            context: context);

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
    public async Task DirectPdfOpen_FuzzyMatch_RegistersSelectedPdfAsNewBook()
    {
        string libraryRoot = Path.Combine(_tempRoot, "library");
        string externalRoot = Path.Combine(_tempRoot, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        string pdfPath = Path.Combine(externalRoot, "same-stat-different-content.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-1.4\n% selected explicit open\n"u8.ToArray());
        var selectedInfo = new FileInfo(pdfPath);

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "EXISTING-FUZZY",
            Status = 0,
            Sha256Hash = new string('b', 64),
            SizeBytes = selectedInfo.Length,
            MtimeTicks = selectedInfo.LastWriteTimeUtc.Ticks,
        });
        await context.SaveChangesAsync();

        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context),
            context: context);

        string bookId = await service.OpenAsync(pdfPath);

        Assert.NotEqual("EXISTING-FUZZY", bookId);
        Assert.Equal(2, context.Books.Count());
        Assert.Equal(new string('b', 64), context.Books.Single(b => b.BookId == "EXISTING-FUZZY").Sha256Hash);

        var file = context.BookFiles.Single(f => f.BookId == bookId);
        Assert.Equal(pdfPath.Replace(Path.DirectorySeparatorChar, '/'), file.RelativePath);
    }

    [Fact]
    public async Task DirectPdfOpen_SameHashAtUnregisteredPath_RegistersSelectedPdfAsNewBook()
    {
        string libraryRoot = Path.Combine(_tempRoot, "library");
        string externalRoot = Path.Combine(_tempRoot, "external");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(externalRoot);

        byte[] content = "%PDF-1.4\n% duplicate direct open test\n"u8.ToArray();
        string existingPath = Path.Combine(libraryRoot, "already-known.pdf");
        string selectedPath = Path.Combine(externalRoot, "selected-copy.pdf");
        await File.WriteAllBytesAsync(existingPath, content);
        await File.WriteAllBytesAsync(selectedPath, content);
        string sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "EXISTING-HASH",
            Status = 0,
            Sha256Hash = sha256,
        });
        context.BookFiles.Add(new BookFileRow
        {
            BookId = "EXISTING-HASH",
            RelativePath = "already-known.pdf",
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await context.SaveChangesAsync();

        var settings = new LibrarySettingsService(_tempRoot);
        await settings.SetLibraryRootAsync(libraryRoot);

        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context),
            context: context);

        string bookId = await service.OpenAsync(selectedPath);

        Assert.NotEqual("EXISTING-HASH", bookId);
        Assert.Equal(2, context.Books.Count());
        Assert.Equal("already-known.pdf", context.BookFiles.Single(f => f.BookId == "EXISTING-HASH").RelativePath);
        Assert.Equal(
            selectedPath.Replace(Path.DirectorySeparatorChar, '/'),
            context.BookFiles.Single(f => f.BookId == bookId).RelativePath);
    }

    [Fact]
    public async Task DirectPdfOpen_MetadataExtraction_VisibleInCatalogueProjection()
    {
        string pdfPath = Path.Combine(_tempRoot, "metadata.pdf");
        await File.WriteAllBytesAsync(pdfPath, CreatePdfWithInfo("Direct Metadata Title", "Direct Metadata Author"));

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        var settings = new LibrarySettingsService(_tempRoot);
        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context),
            context: context);

        string bookId = await service.OpenAsync(pdfPath);
        var extraction = new MetadataExtractionService(context);

        (bool success, string? errorMessage) = await extraction.ExtractAsync(bookId, pdfPath);

        Assert.True(success, errorMessage);

        var readModel = new CatalogueReadModel(context);
        BookDetailProjection? detail = await readModel.GetBookDetailAsync(bookId);

        Assert.NotNull(detail);
        Assert.Equal("Direct Metadata Title", detail.Title);
        Assert.Equal(["Direct Metadata Author"], detail.Authors);

        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
            new CatalogueFilter(TitleContains: "Direct Metadata")))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection visible = Assert.Single(summaries);
        Assert.Equal(bookId, visible.BookId);
        Assert.Equal("Direct Metadata Title", visible.Title);
        Assert.Equal(["Direct Metadata Author"], visible.Authors);
    }

    [Fact]
    public async Task DirectPdfOpen_RepairsMissingBookFilesTableBeforeRegisteringSelectedPdf()
    {
        string pdfPath = Path.Combine(_tempRoot, "repair-open.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-1.4\n% repair direct open test\n"u8.ToArray());

        string dbPath = Path.Combine(_tempRoot, "repair-open.db");
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False")
            .Options;

        await using var context = new CatalogueDbContext(options);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
        await context.Database.ExecuteSqlRawAsync("DROP TABLE BookFiles;");
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

        var settings = new LibrarySettingsService(_tempRoot);
        var service = new DirectPdfOpenService(
            settings,
            new BookIdentityService(context),
            new BookRegistrationService(context),
            new CatalogueMigrator(context),
            context: context);

        string bookId = await service.OpenAsync(pdfPath);

        Assert.Equal(1, await context.BookFiles.CountAsync(f => f.BookId == bookId));
        Assert.Equal("repair-open.pdf", await context.BookFiles
            .Where(f => f.BookId == bookId)
            .Select(f => f.RelativePath)
            .SingleAsync());
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
            new BookRegistrationService(context),
            context: context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenAsync(textPath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

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

    private static byte[] CreatePdfWithInfo(string title, string author)
    {
        using var document = new PdfSharp.Pdf.PdfDocument();
        document.Info.Title = title;
        document.Info.Author = author;
        document.AddPage();

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
