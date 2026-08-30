using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 16 tests for visual asset manifests and catalogue exposure.</summary>
public sealed class Phase16VisualAssetTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase16VisualAssetTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Manifest_CustomCoverWinsAndGeneratedReplacementCannotOverwriteIt()
    {
        const string bookId = "PHASE16-ASSET-BOOK";
        const string originalHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string replacementHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Manifest Book", Status = 0 });
        await _context.SaveChangesAsync();

        var service = new VisualAssetService(_context);
        await service.RegisterGeneratedAsync(
            bookId, originalHash, VisualAssetKind.Cover, "default", ".ogma/covers/aa/original.jpg",
            200, 300, "jpg", 1);
        await service.RegisterCustomCoverAsync(
            bookId, ".ogma/covers/custom/locked.png", 400, 600, "png");
        await service.RegisterGeneratedAsync(
            bookId, replacementHash, VisualAssetKind.Cover, "default", ".ogma/covers/bb/replacement.jpg",
            200, 300, "jpg", 2);
        await service.RegisterGeneratedAsync(
            bookId, originalHash, VisualAssetKind.Spine, "default", ".ogma/spines/aa/original.jpg",
            7, 100, "jpg", 1);

        VisualAssetDescriptor? preferred = await service.GetPreferredAsync(bookId, VisualAssetKind.Cover);
        Assert.NotNull(preferred);
        Assert.True(preferred.IsCustom);
        Assert.Equal(".ogma/covers/custom/locked.png", preferred.RelativePath);

        int invalidated = await service.InvalidateGeneratedAsync(bookId, replacementHash);
        Assert.Equal(1, invalidated);
        Assert.Equal(3, await _context.VisualAssetManifests.CountAsync());
    }

    [Fact]
    public async Task CatalogueReadModel_ProjectsPreferredReadyCoverForSummaryAndDetail()
    {
        const string bookId = "PHASE16-READMODEL-BOOK";
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Visible Cover", Status = 0 });
        _context.VisualAssetManifests.Add(new VisualAssetManifestRow
        {
            BookId = bookId,
            Kind = (int)VisualAssetKind.Cover,
            Variant = "default",
            RelativePath = ".ogma/covers/cc/visible.jpg",
            Source = "generated",
            WidthPx = 200,
            HeightPx = 300,
            Format = "jpg",
            GenerationVersion = 1,
            Status = (int)VisualAssetStatus.Ready,
            UpdatedUtc = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(_context);
        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection projectedSummary = Assert.Single(summaries);
        BookDetailProjection? detail = await readModel.GetBookDetailAsync(bookId);

        Assert.Equal(".ogma/covers/cc/visible.jpg", projectedSummary.CoverRelativePath);
        Assert.NotNull(detail);
        Assert.Equal(".ogma/covers/cc/visible.jpg", detail.CoverRelativePath);
    }

    [Fact]
    public async Task Manifest_RejectsAbsoluteAndTraversalPaths()
    {
        const string bookId = "PHASE16-SAFE-PATH-BOOK";
        _context.Books.Add(new BookRow { BookId = bookId, Status = 0 });
        await _context.SaveChangesAsync();
        var service = new VisualAssetService(_context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterCustomCoverAsync(
            bookId, "C:/outside.png", 200, 300, "png"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterCustomCoverAsync(
            bookId, ".ogma/covers/../outside.png", 200, 300, "png"));
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }
}
