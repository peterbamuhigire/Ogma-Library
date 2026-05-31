using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
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
}
