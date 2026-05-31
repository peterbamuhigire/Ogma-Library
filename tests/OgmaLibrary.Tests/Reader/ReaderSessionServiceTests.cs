using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Integration tests for <see cref="ReaderSessionService"/> — FR-READ-001 open/resume.
/// </summary>
public sealed class ReaderSessionServiceTests : IAsyncDisposable
{
    private readonly CatalogueDbContext _db;
    private readonly string _dbPath;
    private readonly ReadingProgressService _progressService;
    private readonly MockPdfRendererFactory _rendererFactory;
    private readonly PageRenderCache _cache;
    private readonly ReaderSessionService _sessionService;

    // Default test book setup.
    private const string TestBookId = "session-test-book-001";
    private const string TestFilePath = "/test/book.pdf";

    public ReaderSessionServiceTests()
    {
        (_db, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _db.Database.Migrate();

        var progressRepo = new ReadingProgressRepository(_db);
        _progressService = new ReadingProgressService(progressRepo);

        _rendererFactory = new MockPdfRendererFactory(10);

        _cache = new PageRenderCache(
            _rendererFactory,
            new StopwatchBenchmarkContext());

        var fileLocator = new StubBookFileLocator(TestBookId, TestFilePath);

        _sessionService = new ReaderSessionService(
            _rendererFactory,
            _progressService,
            fileLocator,
            _cache);
    }

    public async ValueTask DisposeAsync()
    {
        _sessionService.Dispose();
        _cache.Dispose();
        _progressService.Dispose();
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task OpenAsync_NewBook_StartsAtPage0()
    {
        var session = await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal(TestBookId, session.BookId);
        Assert.Equal(0, session.CurrentPageIndex);
    }

    [Fact]
    public async Task OpenAsync_WithPageHint_StartsAtHintPage()
    {
        var session = await _sessionService.OpenAsync(TestBookId, 7, CancellationToken.None);

        Assert.Equal(7, session.CurrentPageIndex);
    }

    [Fact]
    public async Task NavigateToAsync_ChangesCurrentPage()
    {
        await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);

        await _sessionService.NavigateToAsync(5);

        Assert.Equal(5, _sessionService.CurrentSession!.CurrentPageIndex);
    }

    [Fact]
    public async Task NavigateToAsync_PageOutOfRange_ClampsToBounds()
    {
        await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);

        await _sessionService.NavigateToAsync(999); // Way over page count (10 pages)

        Assert.Equal(9, _sessionService.CurrentSession!.CurrentPageIndex);
    }

    [Fact]
    public async Task CloseAsync_ShouldBeNullAfterClose()
    {
        await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);
        await _sessionService.CloseAsync(CancellationToken.None);

        Assert.Null(_sessionService.CurrentSession);
    }

    [Fact]
    public async Task Events_SessionOpened_IsRaisedOnOpen()
    {
        var events = new List<ReaderEvent>();
        using var subscription = (_sessionService as IReaderSessionReadModel)!.Events
            .Subscribe(e => events.Add(e));

        await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);

        Assert.Contains(events, e => e is ReaderEvent.SessionOpened s && s.BookId == TestBookId);
    }

    [Fact]
    public async Task Events_PageChanged_IsRaisedOnNavigation()
    {
        var events = new List<ReaderEvent>();
        using var subscription = (_sessionService as IReaderSessionReadModel)!.Events
            .Subscribe(e => events.Add(e));

        await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);
        await _sessionService.NavigateToAsync(3);

        Assert.Contains(events, e => e is ReaderEvent.PageChanged pc && pc.PageIndex == 3);
    }

    [Fact]
    public async Task ResumeRoundTrip_OpenAtPage7_Close_Reopen_RestoresPage7()
    {
        // FR-READ-001 round-trip: open at page 7, close, reopen, verify page 7 restored.
        // Seed the book.
        await SeedBookAsync(TestBookId);

        // First session: navigate to page 7, close.
        var session1 = await _sessionService.OpenAsync(TestBookId, 7, CancellationToken.None);
        Assert.Equal(7, session1.CurrentPageIndex);
        await _sessionService.CloseAsync(CancellationToken.None);

        // Second session: open without hint — should resume at page 7.
        var session2 = await _sessionService.OpenAsync(TestBookId, null, CancellationToken.None);
        Assert.Equal(7, session2.CurrentPageIndex);
    }

    private async Task SeedBookAsync(string bookId)
    {
        if (!_db.Books.Any(b => b.BookId == bookId))
        {
            _db.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = bookId,
                Title = $"Test Book {bookId}",
            });

            await _db.SaveChangesAsync();
        }
    }

    // ── Stubs ──────────────────────────────────────────────────────────────────

    private sealed class StubBookFileLocator : IBookFileLocator
    {
        private readonly string _bookId;
        private readonly string _path;

        public StubBookFileLocator(string bookId, string path)
        {
            _bookId = bookId;
            _path = path;
        }

        public Task<string?> LocateAsync(string bookId, CancellationToken ct) =>
            Task.FromResult<string?>(bookId == _bookId ? _path : null);
    }
}
