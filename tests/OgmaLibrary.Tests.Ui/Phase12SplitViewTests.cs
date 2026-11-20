using Avalonia.Headless.XUnit;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Sept-23 Phase 12 slice 5: split view with a picker and per-pane sessions (READ-012).</summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public async Task SplitView_TwoBooks_IndependentPages()
    {
        SplitViewViewModel split = CreateSplit(out _, out _);
        await split.ActivateAsync(null);
        SplitPaneViewModel left = split.LeftPane!;
        SplitPaneViewModel right = split.RightPane!;

        await left.OpenAsync("book-a");
        await right.OpenAsync("book-b");
        await left.Reader.GoToPageAsync(5);

        Assert.Equal(5, left.Reader.CurrentPageIndex);
        Assert.Equal(0, right.Reader.CurrentPageIndex);
        Assert.Equal("book-a", left.Reader.BookId);
        Assert.Equal("book-b", right.Reader.BookId);
        Assert.False(split.SyncScroll);
    }

    [AvaloniaFact]
    public async Task SplitView_Picker_FiltersCatalogue()
    {
        var picker = new BookPickerViewModel(new TitledCatalogue(), new InMemoryLocalizationService());
        BookPickerItem? picked = null;
        picker.BookPicked += (_, item) => picked = item;

        picker.Query = "ocean";
        await picker.RefreshAsync();

        Assert.Single(picker.Results);
        Assert.Equal("book-b", picker.Results[0].BookId);
        Assert.True(picker.HasResults);

        picker.Query = "zzz-none";
        await picker.RefreshAsync();
        Assert.Empty(picker.Results);
        Assert.True(picker.IsEmptyVisible);

        picker.Query = "Smith";
        await picker.RefreshAsync();
        Assert.Equal(["book-a"], picker.Results.Select(r => r.BookId));

        picker.Pick(picker.Results[0]);
        Assert.Equal("book-a", picked?.BookId);
        Assert.Empty(picker.Results);
    }

    [AvaloniaFact]
    public async Task SplitView_Close_DisposesPaneSessions()
    {
        SplitViewViewModel split = CreateSplit(out List<PaneSessions> sessions, out List<PaneScope> scopes);
        await split.ActivateAsync("book-a");
        await split.ActivateAsync("book-a");

        Assert.Equal(SplitViewViewModel.MaxPanes, split.Panes.Count);
        Assert.Equal(2, scopes.Count);
        Assert.Equal("book-a", split.LeftPane!.Reader.BookId);

        await split.CloseAsync();

        Assert.Empty(split.Panes);
        Assert.All(scopes, s => Assert.True(s.Disposed));
        Assert.True(sessions[0].Closed);
        Assert.False(sessions[1].Closed); // never opened, so nothing to close
    }

    private static SplitViewViewModel CreateSplit(out List<PaneSessions> sessions, out List<PaneScope> scopes)
    {
        var localization = new InMemoryLocalizationService();
        List<PaneSessions> madeSessions = [];
        List<PaneScope> madeScopes = [];
        sessions = madeSessions;
        scopes = madeScopes;
        return new SplitViewViewModel(localization, () =>
        {
            var session = new PaneSessions();
            var scope = new PaneScope();
            madeSessions.Add(session);
            madeScopes.Add(scope);
            var reader = new ReaderViewModel(
                session,
                new EmptyAnnotationService(),
                new EmptyBookmarkService(),
                new EmptyLayerService(),
                new EmptyCitationService(),
                new EmptyReadingMemoryService(),
                localization,
                bookIdentity: (_, _) => Task.FromResult<(string Hash, string? Title)?>((new string('b', 64), "Book")));
            return new SplitPaneViewModel(reader, new BookPickerViewModel(new TitledCatalogue(), localization), scope);
        });
    }

    private sealed class PaneScope : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class PaneSessions : IReaderSessionService
    {
        public ReaderSession? CurrentSession { get; private set; }

        public IPdfRenderer? CurrentRenderer => null;

        public bool Closed { get; private set; }

        public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct)
        {
            CurrentSession = new ReaderSession(
                bookId, bookId + ".pdf", PageCount: 10, CurrentPageIndex: pageHint ?? 0, ScrollOffset: 0,
                ZoomMode.FitWidth, ZoomPercent: 100, DisplayMode.SinglePage);
            return Task.FromResult(CurrentSession);
        }

        public Task CloseAsync(CancellationToken ct)
        {
            Closed = true;
            CurrentSession = null;
            return Task.CompletedTask;
        }

        public Task NavigateToAsync(int pageIndex, double scrollOffset = 0)
        {
            CurrentSession = CurrentSession! with { CurrentPageIndex = pageIndex };
            return Task.CompletedTask;
        }

        public void UpdateScrollOffset(double scrollOffset)
        {
        }
    }

    private sealed class TitledCatalogue : ICatalogueReadModel
    {
        private static readonly BookSummaryProjection[] Books =
        [
            new("book-a", "Rivers of Ink", ["Ann Smith"], null, 0, null, [], null, true, 2001),
            new("book-b", "Ocean Atlas", ["Bo Jones"], null, 0, null, [], null, true, 2005),
        ];

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in Books)
            {
                bool title = filter.TitleContains is null ||
                    (book.Title ?? string.Empty).Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase);
                bool author = filter.AuthorContains is null ||
                    book.Authors.Any(a => a.Contains(filter.AuthorContains, StringComparison.OrdinalIgnoreCase));
                if (title && author)
                {
                    yield return book;
                }
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
