using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Commands;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 10, task 10.8 (CAT-005): bulk edit on SQLite. The preview equals the applied
/// result, undo restores every field a bulk edit can change, and cancelling writes nothing.
/// </summary>
public sealed class Sept23Phase10BulkEditTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly CatalogueWriteService _writeService;
    private readonly CatalogueReadModel _readModel;

    /// <summary>Creates a fresh temporary SQLite catalogue.</summary>
    public Sept23Phase10BulkEditTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        _writeService = new CatalogueWriteService(_context, new AuditRepository(_context));
        _readModel = new CatalogueReadModel(_context);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task Preview_EqualsAppliedResult()
    {
        string a = await AddBookAsync("Alpha", year: 1999, rating: 2, author: "Old Writer");
        string b = await AddBookAsync("Beta");
        string c = await AddBookAsync("Gamma", year: 2001);
        var command = Command([a, b, c], author: "  Ada   Lovelace ; Charles Babbage", year: 1843, rating: 4);

        BulkEditPlan plan = BulkEditPlanner.Plan(command, await SummariesAsync());
        Assert.True(plan.CanApply);
        Assert.Equal(9, plan.Rows.Count);

        await _writeService.BulkEditAsync(command);

        Dictionary<string, BookSummaryProjection> after = (await SummariesAsync()).ToDictionary(s => s.BookId);
        foreach (BulkEditPreviewRow row in plan.Rows)
        {
            BookSummaryProjection book = after[row.BookId];
            string? applied = row.Field switch
            {
                BulkEditField.Author => BulkEditFieldRules.FormatAuthors(book.Authors),
                BulkEditField.Year => BulkEditPlanner.Format(book.Year),
                _ => BulkEditPlanner.Format(book.Rating),
            };
            Assert.Equal(row.After, applied);
        }

        Assert.Equal("Ada Lovelace; Charles Babbage", plan.Rows[0].After);
        Assert.Equal("Old Writer", plan.Rows[0].Before);
    }

    [Fact]
    public async Task Undo_RestoresAuthorYearStatusRatingTagsAndShelf()
    {
        string shelfKeep = await _writeService.CreateShelfAsync("Keep");
        string shelfNew = await _writeService.CreateShelfAsync("New");
        string a = await AddBookAsync("Alpha", year: 1999, rating: 2, author: "Old Writer");
        string b = await AddBookAsync("Beta");
        await _writeService.AddBookToShelfAsync(shelfKeep, a);
        await _writeService.BulkEditAsync(Command([a], tagsToAdd: ["kept"]));
        _context.ChangeTracker.Clear();
        string before = await StateAsync([a, b]);

        var history = new CommandHistory();
        var edit = new BulkEditUndoableCommand(
            _writeService,
            new BulkEditCommand(
                [a, b],
                ["added"],
                ["kept"],
                shelfNew,
                shelfKeep,
                NewLifecycleStatus: 2,
                NewRating: 5,
                AuthorToSet: "New Author",
                YearToSet: 2020));
        await history.ExecuteAsync(edit);
        _context.ChangeTracker.Clear();
        Assert.NotEqual(before, await StateAsync([a, b]));

        await history.UndoAsync();
        _context.ChangeTracker.Clear();

        Assert.Equal(before, await StateAsync([a, b]));
        Assert.False(history.CanUndo);
        Assert.Equal(["Old Writer"], (await SummariesAsync()).Single(s => s.BookId == a).Authors);
        Assert.Empty((await SummariesAsync()).Single(s => s.BookId == b).Authors);
    }

    [Fact]
    public async Task PlanWithoutApply_WritesNothing()
    {
        string a = await AddBookAsync("Alpha", year: 1999);
        _context.ChangeTracker.Clear();
        string before = await StateAsync([a]);
        int audits = await _context.AuditEvents.CountAsync();

        BulkEditPlan plan = BulkEditPlanner.Plan(Command([a], author: "Someone", year: 2000), await SummariesAsync());

        Assert.True(plan.CanApply);
        Assert.Equal(before, await StateAsync([a]));
        Assert.Equal(audits, await _context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Plan_WithNoChange_CannotApply_AndInvalidValuesAreRejected()
    {
        string a = await AddBookAsync("Alpha", year: 1999, author: "Old Writer");
        IReadOnlyList<BookSummaryProjection> books = await SummariesAsync();

        Assert.False(BulkEditPlanner.Plan(Command([a], author: "Old Writer", year: 1999), books).CanApply);
        Assert.False(BulkEditPlanner.Plan(Command([a], author: " ; "), books).IsValid);
        Assert.False(BulkEditPlanner.Plan(Command([a], year: 0), books).IsValid);
        await Assert.ThrowsAsync<ArgumentException>(() => _writeService.BulkEditAsync(Command([a], year: 10000)));
        await Assert.ThrowsAsync<ArgumentException>(() => _writeService.UpdateMetadataFieldAsync(a, "Year", "19x9"));
    }

    private static BulkEditCommand Command(
        IReadOnlyList<string> ids,
        string? author = null,
        int? year = null,
        int? rating = null,
        IReadOnlyList<string>? tagsToAdd = null) =>
        new(ids, tagsToAdd ?? [], [], null, null, null, rating, author, year);

    private async Task<string> AddBookAsync(string title, int? year = null, int? rating = null, string? author = null)
    {
        string bookId = Guid.NewGuid().ToString("N");
        var book = new BookRow { BookId = bookId, Title = title, Status = 0, Year = year, Rating = rating };
        if (author is not null)
        {
            var authorRow = await _context.Authors.FirstOrDefaultAsync(x => x.NormalizedName == author)
                ?? new AuthorRow { NormalizedName = author, SortName = author };
            book.BookAuthors.Add(new BookAuthorRow { BookId = bookId, Author = authorRow, Role = "Author", DisplayOrder = 0 });
        }

        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return bookId;
    }

    private async Task<IReadOnlyList<BookSummaryProjection>> SummariesAsync()
    {
        var list = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in _readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            list.Add(summary);
        }

        return list;
    }

    // A canonical text form of every field a bulk edit can change, read straight from SQLite.
    private async Task<string> StateAsync(IReadOnlyList<string> ids)
    {
        var books = await _context.Books.AsNoTracking()
            .Where(b => ids.Contains(b.BookId))
            .OrderBy(b => b.BookId)
            .Select(b => new
            {
                b.BookId,
                b.Year,
                b.Status,
                b.Rating,
                Authors = b.BookAuthors.OrderBy(x => x.DisplayOrder).Select(x => x.Author!.NormalizedName).ToList(),
                Fields = b.MetadataFields
                    .Where(f => f.Source == "User")
                    .OrderBy(f => f.FieldName)
                    .Select(f => f.FieldName + "=" + f.Value)
                    .ToList(),
                Shelves = b.ShelfBooks.OrderBy(s => s.ShelfId).Select(s => s.ShelfId).ToList(),
            })
            .ToListAsync();
        return string.Join(
            "\n",
            books.Select(b =>
                $"{b.BookId}|{b.Year}|{b.Status}|{b.Rating}|{string.Join(",", b.Authors)}|{string.Join(",", b.Fields)}|{string.Join(",", b.Shelves)}"));
    }
}
