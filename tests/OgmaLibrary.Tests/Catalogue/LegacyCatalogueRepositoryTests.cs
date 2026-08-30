using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Integration tests for the pre-canonical compatibility adapter against SQLite.
/// </summary>
public sealed class LegacyCatalogueRepositoryTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly LegacyCatalogueRepository _repository;

    public LegacyCatalogueRepositoryTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _repository = new LegacyCatalogueRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task BookRepository_SaveAndLoad_RoundTrips()
    {
        var bookId = new BookId("01HZZZZZZZZZZZZZZZZZZZZZZY");
        var book = new LegacyCatalogueRecord
        {
            Id = bookId,
            Title = "Integration Test Book",
            Year = 2024,
            Rating = 4,
        };

        await _repository.SaveAsync(book, CancellationToken.None);

        LegacyCatalogueRecord? loaded = await _repository.FindAsync(bookId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Integration Test Book", loaded.Title);
        Assert.Equal(2024, loaded.Year);
        Assert.Equal(4, loaded.Rating);
        Assert.Equal(bookId.Value, loaded.Id.Value);
    }

    [Fact]
    public async Task BookRepository_FindAsync_ReturnsNull_WhenBookNotFound()
    {
        var missingId = new BookId("01HZZZZZZZZZZZZZZZZZZZZZZX");

        LegacyCatalogueRecord? result = await _repository.FindAsync(missingId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BookRepository_SaveAsync_UpdatesExistingBook()
    {
        var bookId = new BookId("01HZZZZZZZZZZZZZZZZZZZZZZA");
        var book = new LegacyCatalogueRecord
        {
            Id = bookId,
            Title = "Original Title",
            Year = 2020,
        };
        await _repository.SaveAsync(book, CancellationToken.None);

        // Simulate updating the book.
        var updated = new LegacyCatalogueRecord
        {
            Id = bookId,
            Title = "Updated Title",
            Year = 2021,
            Rating = 5,
        };
        await _repository.SaveAsync(updated, CancellationToken.None);

        LegacyCatalogueRecord? loaded = await _repository.FindAsync(bookId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Updated Title", loaded.Title);
        Assert.Equal(5, loaded.Rating);
    }

    [Fact]
    public async Task LegacyAdapter_DoesNotFabricateUnknownFileFacts()
    {
        const string bookId = "01HZZZZZZZZZZZZZZZZZZZZZZB";
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Unknown identity",
            Sha256Hash = "not-a-hash",
            Status = 0,
            BookFiles =
            [
                new BookFileRow
                {
                    BookId = bookId,
                    RelativePath = "unknown.pdf",
                    FileStatus = 0,
                },
            ],
        });
        await _context.SaveChangesAsync();

        LegacyCatalogueRecord loaded = Assert.IsType<LegacyCatalogueRecord>(
            await _repository.FindAsync(new BookId(bookId), CancellationToken.None));
        LegacyFileRecord file = Assert.Single(loaded.Files);

        Assert.Null(file.ContentHash);
        Assert.Null(file.SizeBytes);
        Assert.Null(file.ModifiedUtc);
    }

    [Fact]
    public async Task LegacyAdapter_MapsOnlyPersistedVerifiedFileFacts()
    {
        const string bookId = "01HZZZZZZZZZZZZZZZZZZZZZZC";
        string hash = new('b', 64);
        long modifiedTicks = DateTimeOffset.UtcNow.Ticks;
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Verified identity",
            Sha256Hash = hash,
            SizeBytes = 1234,
            MtimeTicks = modifiedTicks,
            Status = 0,
            BookFiles =
            [
                new BookFileRow
                {
                    BookId = bookId,
                    RelativePath = "verified.pdf",
                    FileStatus = 0,
                },
            ],
        });
        await _context.SaveChangesAsync();

        LegacyCatalogueRecord loaded = Assert.IsType<LegacyCatalogueRecord>(
            await _repository.FindAsync(new BookId(bookId), CancellationToken.None));
        LegacyFileRecord file = Assert.Single(loaded.Files);

        Assert.Equal(hash, file.ContentHash?.Hex);
        Assert.Equal(1234, file.SizeBytes);
        Assert.Equal(modifiedTicks, file.ModifiedUtc?.Ticks);
    }
}
