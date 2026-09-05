using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase06;

/// <summary>
/// Integration tests for FR-CAT-003: shelf CRUD, multi-shelf membership, and
/// smart shelf evaluation.
/// </summary>
public sealed class ShelfTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly CatalogueWriteService _writeService;
    private readonly CatalogueReadModel _readModel;
    private readonly AuditRepository _audit;

    /// <summary>Creates a fresh isolated in-memory context for each test.</summary>
    public ShelfTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();

        _audit = new AuditRepository(_context);
        _writeService = new CatalogueWriteService(_context, _audit);
        _readModel = new CatalogueReadModel(_context);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> AddBookAsync(string title = "Test Book")
    {
        string bookId = Guid.NewGuid().ToString("N");
        _context.Books.Add(new BookRow { BookId = bookId, Title = title, Status = 0 });
        await _context.SaveChangesAsync();
        return bookId;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// FR-CAT-003: A book can belong to multiple shelves independently of its file path.
    /// Removing from one shelf keeps the other memberships intact.
    /// </summary>
    [Fact]
    public async Task Shelf_BookInMultipleShelves_NoPathDependency()
    {
        string bookId = await AddBookAsync();

        string shelf1 = await _writeService.CreateShelfAsync("Shelf A");
        string shelf2 = await _writeService.CreateShelfAsync("Shelf B");
        string shelf3 = await _writeService.CreateShelfAsync("Shelf C");

        await _writeService.AddBookToShelfAsync(shelf1, bookId);
        await _writeService.AddBookToShelfAsync(shelf2, bookId);
        await _writeService.AddBookToShelfAsync(shelf3, bookId);

        // Verify all 3 ShelfBook rows exist.
        int count = await _context.ShelfBooks.CountAsync(sb => sb.BookId == bookId);
        Assert.Equal(3, count);

        // Remove from shelf 2 — shelf 1 and 3 should still be there.
        await _writeService.RemoveBookFromShelfAsync(shelf2, bookId);

        Assert.True(await _context.ShelfBooks.AnyAsync(sb => sb.ShelfId == shelf1 && sb.BookId == bookId));
        Assert.False(await _context.ShelfBooks.AnyAsync(sb => sb.ShelfId == shelf2 && sb.BookId == bookId));
        Assert.True(await _context.ShelfBooks.AnyAsync(sb => sb.ShelfId == shelf3 && sb.BookId == bookId));
    }

    /// <summary>
    /// FR-CAT-003: Adding a book to a shelf twice is idempotent.
    /// </summary>
    [Fact]
    public async Task Shelf_AddBookTwice_IsIdempotent()
    {
        string bookId = await AddBookAsync();
        string shelfId = await _writeService.CreateShelfAsync("My Shelf");

        await _writeService.AddBookToShelfAsync(shelfId, bookId);
        await _writeService.AddBookToShelfAsync(shelfId, bookId); // second call

        int count = await _context.ShelfBooks.CountAsync(sb => sb.ShelfId == shelfId && sb.BookId == bookId);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// FR-CAT-003: Deleting a shelf removes all its ShelfBook rows.
    /// </summary>
    [Fact]
    public async Task Shelf_Delete_RemovesAllShelfBooks()
    {
        string bookId = await AddBookAsync();
        string shelfId = await _writeService.CreateShelfAsync("Doomed Shelf");

        await _writeService.AddBookToShelfAsync(shelfId, bookId);
        Assert.Equal(1, await _context.ShelfBooks.CountAsync(sb => sb.ShelfId == shelfId));

        await _writeService.DeleteShelfAsync(shelfId);

        Assert.Equal(0, await _context.ShelfBooks.CountAsync(sb => sb.ShelfId == shelfId));
        Assert.Equal(0, await _context.Shelves.CountAsync(s => s.ShelfId == shelfId));
    }

    /// <summary>
    /// FR-CAT-003: Shelf rename updates the display name.
    /// </summary>
    [Fact]
    public async Task Shelf_Rename_UpdatesDisplayName()
    {
        string shelfId = await _writeService.CreateShelfAsync("Old Name");
        await _writeService.RenameShelfAsync(shelfId, "New Name");

        var shelf = await _context.Shelves.FirstAsync(s => s.ShelfId == shelfId);
        Assert.Equal("New Name", shelf.Name);
    }

    [Fact]
    public async Task SmartShelf_Create_ValidatesAndPersistsClosedQueryContract()
    {
        string query = JsonSerializer.Serialize(new[]
        {
            new SmartShelfCondition(
                SmartShelfField.Rating,
                SmartShelfOperator.GreaterThanOrEqual,
                "4"),
        });

        string shelfId = await _writeService.CreateShelfAsync(
            "Highly Rated",
            isSmart: true,
            query: query);

        ShelfRow shelf = await _context.Shelves.SingleAsync(row => row.ShelfId == shelfId);
        Assert.Equal(1, shelf.ShelfType);
        Assert.Equal(query, shelf.Query);

        await Assert.ThrowsAsync<ArgumentException>(() => _writeService.CreateShelfAsync(
            "Damaged Smart Shelf",
            isSmart: true,
            query: "{\"status\":0}"));
    }

    [Fact]
    public async Task BulkEdit_Tags_AddsAndRemovesNormalizedUserTags()
    {
        string bookId = await AddBookAsync();

        await _writeService.BulkEditAsync(new BulkEditCommand(
            [bookId],
            ["Systems", "reader", "systems"],
            [],
            null,
            null,
            null,
            null));

        BookMetadataFieldRow tags = await _context.BookMetadataFields
            .SingleAsync(field => field.BookId == bookId && field.FieldName == "Tags");
        Assert.Equal("reader; Systems", tags.Value);
        Assert.Equal("User", tags.Source);
        Assert.True(tags.IsOverridden);

        await _writeService.BulkEditAsync(new BulkEditCommand(
            [bookId],
            [],
            ["READER"],
            null,
            null,
            null,
            null));

        tags = await _context.BookMetadataFields
            .SingleAsync(field => field.BookId == bookId && field.FieldName == "Tags");
        Assert.Equal("Systems", tags.Value);
    }
}
