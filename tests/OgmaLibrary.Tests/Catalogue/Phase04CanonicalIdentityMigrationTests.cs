using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 4 migration, constraint, alias and re-entry acceptance tests.</summary>
public sealed class Phase04CanonicalIdentityMigrationTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase04CanonicalIdentityMigrationTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task Migrator_PreservesCuratedFieldsAndCreatesCanonicalAliases()
    {
        await _context.Database.MigrateAsync();
        const string verifiedHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        _context.Books.AddRange(
            new BookRow
            {
                BookId = "01PHASE04BOOK0000000000001",
                Title = "Curated identity",
                Rating = 5,
                Sha256Hash = verifiedHash,
                SizeBytes = 123,
                Status = 0,
                EmbeddingStatus = 2,
                BookFiles =
                [
                    new BookFileRow
                    {
                        RelativePath = "Books/curated.pdf",
                        FileStatus = 0,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    },
                ],
            },
            new BookRow
            {
                BookId = "01PHASE04BOOK0000000000002",
                Title = "Unknown identity",
                Rating = 3,
                Sha256Hash = "not-a-sha256",
                SizeBytes = 0,
                Status = 1,
                EmbeddingStatus = 2,
                BookFiles =
                [
                    new BookFileRow
                    {
                        RelativePath = "Books/unknown.pdf",
                        FileStatus = 1,
                    },
                ],
            });
        await _context.SaveChangesAsync();

        var migrator = new CatalogueMigrator(_context);
        List<CatalogueMigrationProgress> progress = [];
        migrator.ProgressChanged += (_, update) => progress.Add(update);
        await migrator.ApplyAsync();

        Assert.Equal(2, await _context.LegacyIdentityAliases.CountAsync());
        Assert.Equal(2, await _context.CanonicalWorks.CountAsync());
        Assert.Equal(2, await _context.CanonicalEditions.CountAsync());
        Assert.Equal(2, await _context.CatalogueItems.CountAsync());
        Assert.Equal(1, await _context.ContentAssets.CountAsync());
        Assert.Equal(2, await _context.FileOccurrences.CountAsync());
        Assert.Equal(1, await _context.EditionContentAssets.CountAsync());
        Assert.Equal(2, await _context.CatalogueItemOccurrences.CountAsync());

        BookRow curated = await _context.Books.SingleAsync(book => book.BookId.EndsWith('1'));
        BookRow unknown = await _context.Books.SingleAsync(book => book.BookId.EndsWith('2'));
        Assert.Equal(5, curated.Rating);
        Assert.Equal(0, curated.EmbeddingStatus);
        Assert.Equal(3, unknown.Rating);
        Assert.Equal(0, unknown.EmbeddingStatus);

        FileOccurrenceRow unknownOccurrence = await _context.FileOccurrences
            .SingleAsync(occurrence => occurrence.NormalizedRelativePath == "Books/unknown.pdf");
        Assert.Null(unknownOccurrence.ContentAssetId);
        Assert.Equal((int)AvailabilityStatus.Unavailable, unknownOccurrence.AvailabilityStatus);

        CanonicalIdentityRepository repository = new(_context);
        CanonicalIdentityProjection? projection = await repository.FindByLegacyBookIdAsync(
            new BookId("01PHASE04BOOK0000000000001"),
            CancellationToken.None);
        Assert.NotNull(projection);
        Assert.Equal(BibliographicResolutionState.Provisional, projection.WorkResolutionState);
        Assert.Single(projection.Occurrences);
        Assert.True(projection.RequiresSemanticReindex);
        Assert.Single(progress);
        Assert.Equal(2, progress[0].CompletedItems);
        Assert.Equal(2, progress[0].TotalItems);
    }

    [Fact]
    public async Task Migrator_IsRestartableAndDoesNotDuplicateAliases()
    {
        await _context.Database.MigrateAsync();
        _context.Books.Add(new BookRow
        {
            BookId = "01PHASE04BOOK0000000000003",
            Title = "Restartable",
            Status = 0,
            BookFiles =
            [
                new BookFileRow
                {
                    RelativePath = "restartable.pdf",
                    FileStatus = 0,
                },
            ],
        });
        await _context.SaveChangesAsync();

        var migrator = new CatalogueMigrator(_context);
        await migrator.ApplyAsync();
        await migrator.ApplyAsync();

        Assert.Equal(1, await _context.LegacyIdentityAliases.CountAsync());
        Assert.Equal(1, await _context.CanonicalWorks.CountAsync());
        Assert.NotNull(migrator.LastPreflightReport);
        Assert.NotNull(migrator.LastMigrationResult);
        Assert.Equal(0, migrator.LastMigrationResult!.MigratedBooks);
    }

    [Fact]
    public async Task CanonicalSchema_RejectsInvalidIdentityCombinations()
    {
        await _context.Database.MigrateAsync();

        _context.ContentAssets.Add(new ContentAssetRow
        {
            ContentAssetId = "A0000000000000000000000000",
            Sha256Hash = "invalid",
            FingerprintVersion = 1,
            VerificationStatus = 0,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        _context.ChangeTracker.Clear();

        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = "B0000000000000000000000000",
            DisplayName = "root",
            RootStatus = 99,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }
}
