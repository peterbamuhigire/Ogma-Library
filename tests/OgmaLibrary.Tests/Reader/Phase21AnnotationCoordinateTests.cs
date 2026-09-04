using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Reader;

/// <summary>Phase 21 tests for versioned annotation-coordinate compatibility.</summary>
public sealed class Phase21AnnotationCoordinateTests : IDisposable
{
    private const string BookId = "PHASE21-COORDINATE-BOOK";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase21AnnotationCoordinateTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        _context.Books.Add(new BookRow { BookId = BookId, Title = "Coordinate book", Status = 0 });
        _context.SaveChanges();
    }

    [Fact]
    public void MissingVersion_FallsBackToCurrentNormalizedContract()
    {
        Assert.Equal(
            AnnotationCoordinateContract.CurrentVersion,
            AnnotationCoordinateContract.NormalizeVersion(null));
        Assert.Equal(
            AnnotationCoordinateContract.CurrentVersion,
            AnnotationCoordinateContract.NormalizeVersion("  "));
        Assert.True(AnnotationCoordinateContract.IsSupported(null));
    }

    [Fact]
    public async Task Repository_PersistsCurrentCoordinateVersionAndLegacyRowsReloadSafely()
    {
        var repository = new AnnotationV2Repository(_context);
        AnnotationV2 saved = await repository.CreateAsync(new AnnotationV2
        {
            Id = "01JCOORDINATE00000000000001",
            BookId = BookId,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(2, 0.1, 0.2, 0.3, 0.1)],
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        Assert.Equal(AnnotationCoordinateContract.CurrentVersion, saved.CoordinateVersion);
        Assert.Equal(
            AnnotationCoordinateContract.CurrentVersion,
            _context.AnnotationsV2.Single(row => row.AnnotationId == saved.Id).CoordinateVersion);

        _context.AnnotationsV2.Add(new AnnotationV2Row
        {
            AnnotationId = "01JCOORDINATE00000000000002",
            BookId = BookId,
            CoordinateVersion = string.Empty,
            RegionsJson = "[{\"p\":3,\"l\":0.2,\"t\":0.3,\"w\":0.4,\"h\":0.1}]",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        AnnotationV2 legacy = (await repository.FindAsync(
            "01JCOORDINATE00000000000002", CancellationToken.None))!;
        Assert.Equal(AnnotationCoordinateContract.CurrentVersion, legacy.CoordinateVersion);
        Assert.Single(legacy.Regions);
        Assert.Equal(3, legacy.Regions[0].PageIndex);
    }

    [Fact]
    public async Task Repository_UnsupportedCoordinateVersionFailsClosedWithoutRenderingRegions()
    {
        _context.AnnotationsV2.Add(new AnnotationV2Row
        {
            AnnotationId = "01JCOORDINATE00000000000003",
            BookId = BookId,
            CoordinateVersion = "future-v2",
            RegionsJson = "[{\"p\":1,\"l\":0.1,\"t\":0.1,\"w\":0.2,\"h\":0.2}]",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        AnnotationV2 future = (await new AnnotationV2Repository(_context).FindAsync(
            "01JCOORDINATE00000000000003", CancellationToken.None))!;
        Assert.Equal("future-v2", future.CoordinateVersion);
        Assert.Empty(future.Regions);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
