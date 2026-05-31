using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Integration tests for <see cref="BookRepository"/> against a real SQLite file
/// (Phase 04 deliverable 10).
/// </summary>
public sealed class BookRepositoryTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly BookRepository _repository;

    public BookRepositoryTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _repository = new BookRepository(_context);
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
        var book = new Book
        {
            Id = bookId,
            Title = "Integration Test Book",
            Year = 2024,
            Rating = 4,
        };

        await _repository.SaveAsync(book, CancellationToken.None);

        Book? loaded = await _repository.FindAsync(bookId, CancellationToken.None);

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

        Book? result = await _repository.FindAsync(missingId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BookRepository_SaveAsync_UpdatesExistingBook()
    {
        var bookId = new BookId("01HZZZZZZZZZZZZZZZZZZZZZZA");
        var book = new Book
        {
            Id = bookId,
            Title = "Original Title",
            Year = 2020,
        };
        await _repository.SaveAsync(book, CancellationToken.None);

        // Simulate updating the book.
        var updated = new Book
        {
            Id = bookId,
            Title = "Updated Title",
            Year = 2021,
            Rating = 5,
        };
        await _repository.SaveAsync(updated, CancellationToken.None);

        Book? loaded = await _repository.FindAsync(bookId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Updated Title", loaded.Title);
        Assert.Equal(5, loaded.Rating);
    }
}
