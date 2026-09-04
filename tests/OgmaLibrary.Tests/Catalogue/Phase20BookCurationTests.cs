using OgmaLibrary.Domain;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 20 tests for durable personal reading-state curation.</summary>
public sealed class Phase20BookCurationTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase20BookCurationTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        _context.Books.Add(new BookRow
        {
            BookId = "PHASE20-CURATION-BOOK",
            Title = "Curation Book",
            Status = 0,
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task UpdateReadingState_PersistsProgressRatingFavouriteAndRedactedHistory()
    {
        var service = new BookCurationService(_context);

        await service.UpdateReadingStateAsync(
            "PHASE20-CURATION-BOOK",
            ReadingStatus.Reading,
            rating: 5,
            isFavourite: true,
            reason: "opened from detail");

        BookRow book = await _context.Books.SingleAsync(book => book.BookId == "PHASE20-CURATION-BOOK");
        ReadingProgressRow progress = await _context.ReadingProgress.SingleAsync(
            progress => progress.BookId == book.BookId);
        ReadingStateHistoryRow history = await _context.ReadingStateHistory.SingleAsync(
            history => history.BookId == book.BookId);

        Assert.Equal(5, book.Rating);
        Assert.True(book.IsFavourite);
        Assert.Equal(ReadingStatus.Reading, (ReadingStatus)progress.Status);
        Assert.Equal(ReadingStatus.Reading, history.ReadingStatus);
        Assert.Equal(5, history.Rating);
        Assert.True(history.IsFavourite);
        Assert.Equal("opened from detail", history.Reason);

        var readModel = new CatalogueReadModel(_context);
        BookDetailProjection? detail = await readModel.GetBookDetailAsync(book.BookId);
        Assert.NotNull(detail);
        Assert.True(detail.IsFavourite);
    }

    [Fact]
    public async Task UpdateReadingState_RejectsInvalidRatingAndUnknownBook()
    {
        var service = new BookCurationService(_context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpdateReadingStateAsync(
            "PHASE20-CURATION-BOOK", rating: 6));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateReadingStateAsync(
            "PHASE20-MISSING-BOOK", isFavourite: true));
    }

    [Fact]
    public async Task GetHistory_ReturnsNewestFirstAndHonoursBound()
    {
        var service = new BookCurationService(_context);

        await service.UpdateReadingStateAsync(
            "PHASE20-CURATION-BOOK",
            readingStatus: ReadingStatus.Reading,
            reason: "opened from detail");
        await service.UpdateReadingStateAsync(
            "PHASE20-CURATION-BOOK",
            readingStatus: ReadingStatus.Finished,
            reason: "finished book");

        IReadOnlyList<ReadingStateHistoryEntry> history = await service.GetHistoryAsync(
            "PHASE20-CURATION-BOOK",
            maxResults: 1);

        ReadingStateHistoryEntry entry = Assert.Single(history);
        Assert.Equal(ReadingStatus.Finished, entry.ReadingStatus);
        Assert.Equal("finished book", entry.Reason);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }
}
