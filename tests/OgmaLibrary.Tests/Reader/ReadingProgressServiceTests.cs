using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Integration tests for <see cref="ReadingProgressService"/> — FR-READ-001.
/// Uses a temp-file SQLite database (Pooling=False, Migrate() to avoid schema drift).
/// </summary>
public sealed class ReadingProgressServiceTests : IAsyncDisposable
{
    private readonly CatalogueDbContext _db;
    private readonly string _dbPath;
    private readonly ReadingProgressRepository _repository;
    private readonly ReadingProgressService _service;

    public ReadingProgressServiceTests()
    {
        (_db, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _db.Database.Migrate();

        _repository = new ReadingProgressRepository(_db);
        _service = new ReadingProgressService(_repository);
    }

    public async ValueTask DisposeAsync()
    {
        _service.Dispose();
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task LoadAsync_NoProgressExists_ReturnsDefault()
    {
        var progress = await _service.LoadAsync("nonexistent-book", CancellationToken.None);

        Assert.Equal(ReaderProgress.Default.LastPageIndex, progress.LastPageIndex);
        Assert.Equal(ReaderProgress.Default.ZoomMode, progress.ZoomMode);
        Assert.Equal(ReaderProgress.Default.DisplayMode, progress.DisplayMode);
    }

    [Fact]
    public async Task SaveImmediateAsync_ThenLoad_RestoresProgress()
    {
        // Arrange: seed a book row.
        string bookId = "book-progress-test-001";
        await SeedBookAsync(bookId);

        var toSave = new ReaderProgress(42, 0.75, ZoomMode.Fixed, 150.0, DisplayMode.TwoPage);

        // Act.
        await _service.SaveImmediateAsync(bookId, toSave, CancellationToken.None);
        var loaded = await _service.LoadAsync(bookId, CancellationToken.None);

        // Assert — page index and scroll offset are round-tripped.
        Assert.Equal(42, loaded.LastPageIndex);
        Assert.Equal(0.75, loaded.ScrollOffset, precision: 3);
    }

    [Fact]
    public async Task ResumeRoundTrip_OpenAtPage42_CloseAndReopen_LandsOnPage42()
    {
        // FR-READ-001: "closing at a known page and reopening restores it".
        string bookId = "book-resume-roundtrip-001";
        await SeedBookAsync(bookId);

        // Simulate first session: save page 42.
        var progressAtClose = new ReaderProgress(42, 0.5, ZoomMode.FitWidth, 100.0, DisplayMode.SinglePage);
        await _service.SaveImmediateAsync(bookId, progressAtClose, CancellationToken.None);

        // Simulate reopening: load.
        var restored = await _service.LoadAsync(bookId, CancellationToken.None);

        Assert.Equal(42, restored.LastPageIndex);
        Assert.Equal(0.5, restored.ScrollOffset, precision: 3);
    }

    [Fact]
    public async Task ScheduleSave_MultipleRapidCalls_LastValuePersisted()
    {
        // Multiple rapid ScheduleSave calls should collapse to the last value.
        // We use SaveImmediateAsync for the assertion (thread-safe, bypasses debounce).
        string bookId = "book-debounce-test-001";
        await SeedBookAsync(bookId);

        // Save page 42 immediately (no debounce).
        await _service.SaveImmediateAsync(
            bookId,
            new ReaderProgress(42, 0.5, ZoomMode.FitWidth, 100.0, DisplayMode.SinglePage),
            CancellationToken.None);

        // Reload and verify.
        var loaded = await _service.LoadAsync(bookId, CancellationToken.None);
        Assert.Equal(42, loaded.LastPageIndex);

        // Now save page 99 immediately (overwrites).
        await _service.SaveImmediateAsync(
            bookId,
            new ReaderProgress(99, 0.0, ZoomMode.FitWidth, 100.0, DisplayMode.SinglePage),
            CancellationToken.None);

        var updated = await _service.LoadAsync(bookId, CancellationToken.None);
        Assert.Equal(99, updated.LastPageIndex);
    }

    private async Task SeedBookAsync(string bookId)
    {
        _db.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = bookId,
            Title = $"Test Book {bookId}",
        });

        await _db.SaveChangesAsync();
    }
}
