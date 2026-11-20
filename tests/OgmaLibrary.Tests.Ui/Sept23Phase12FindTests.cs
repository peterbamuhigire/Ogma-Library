using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 12, slice 3: the find bar, the shared navigation history and the Contents tab.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    private const string FindBookId = "book-find";

    [AvaloniaFact]
    public async Task FindBar_States_EmptyNoMatchMatches()
    {
        (ReaderViewModel reader, _) = await OpenFindReaderAsync();
        reader.FindDebounce = TimeSpan.Zero;

        reader.OpenFind();
        Assert.True(reader.IsFindOpen);
        Assert.Equal(FindBarState.Empty, reader.FindState);

        reader.FindQuery = "zzzz-not-there";
        await reader.WaitForFindAsync();
        Assert.Equal(FindBarState.NoMatch, reader.FindState);
        Assert.Empty(reader.FindResults);
        Assert.False(string.IsNullOrWhiteSpace(reader.FindCountText));

        reader.FindQuery = "caravan";
        await reader.WaitForFindAsync();
        Assert.Equal(FindBarState.Matches, reader.FindState);
        Assert.Equal(3, reader.FindMatchCount);

        reader.FindQuery = string.Empty;
        await reader.WaitForFindAsync();
        Assert.Equal(FindBarState.Empty, reader.FindState);
        Assert.Empty(reader.FindHighlights);

        reader.CloseFind();
        Assert.False(reader.IsFindOpen);
    }

    [AvaloniaFact]
    public async Task Find_CaravanOnTradeRoutes_HighlightsEveryMatch()
    {
        (ReaderViewModel reader, _) = await OpenFindReaderAsync();
        reader.FindDebounce = TimeSpan.Zero;
        reader.OpenFind();
        reader.FindQuery = "Caravan";
        await reader.WaitForFindAsync();

        // Pages 2 (two hits) and 5 (one hit): the first match is current and shown.
        Assert.Equal([2, 5], reader.FindResults.Select(r => r.PageIndex));
        Assert.Equal(2, reader.CurrentPageIndex);
        Assert.Equal(2, reader.FindHighlights.Count);
        Assert.Single(reader.FindHighlights, h => h.IsCurrent);

        await reader.FindNextAsync();
        await reader.FindNextAsync();
        Assert.Equal(5, reader.CurrentPageIndex);
        Assert.Single(reader.FindHighlights);
        Assert.True(reader.FindHighlights[0].IsCurrent);

        await reader.FindNextAsync();
        Assert.Equal(2, reader.CurrentPageIndex);
        await reader.FindPreviousAsync();
        Assert.Equal(5, reader.CurrentPageIndex);
    }

    [AvaloniaFact]
    public async Task History_TocJumpThenBack_ReturnsPriorPage()
    {
        (ReaderViewModel reader, _) = await OpenFindReaderAsync();
        await reader.LoadContentsAsync();
        Assert.Equal(2, reader.ContentsEntries.Count);
        Assert.False(reader.CanHistoryBack);

        await reader.GoNextAsync();
        Assert.Equal(1, reader.CurrentPageIndex);
        await reader.JumpToContentsEntryAsync(reader.ContentsEntries[1]);
        Assert.Equal(8, reader.CurrentPageIndex);
        Assert.True(reader.CanHistoryBack);

        await reader.HistoryBackAsync();
        Assert.Equal(1, reader.CurrentPageIndex);
        Assert.True(reader.CanHistoryForward);
        await reader.HistoryForwardAsync();
        Assert.Equal(8, reader.CurrentPageIndex);
    }

    [AvaloniaFact]
    public async Task GoToPage_CtrlG_PushesHistory()
    {
        (ReaderViewModel reader, _) = await OpenFindReaderAsync();

        reader.PageNumberInput = "10";
        await reader.JumpToPageAsync();
        Assert.Equal(9, reader.CurrentPageIndex);
        Assert.True(reader.CanHistoryBack);

        await reader.HistoryBackAsync();
        Assert.Equal(0, reader.CurrentPageIndex);
    }

    [AvaloniaFact]
    public async Task ContentsTab_NoOutline_ShowsEmptyState()
    {
        var sessions = new FakeReaderSessionService();
        ReaderViewModel reader = NewFindReader(sessions, search: null);
        Assert.True(await reader.OpenAsync(FindBookId, 0, CancellationToken.None));
        await reader.LoadContentsAsync();

        Assert.Empty(reader.ContentsEntries);
        Assert.True(reader.ShowContentsEmptyState);
        Assert.False(string.IsNullOrWhiteSpace(reader.ContentsEmptyText));
    }

    [AvaloniaFact]
    public async Task ReaderView_FindHistoryAndContents_AreNamedAndReachable()
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(1280, 760);
        shell.Reader!.OpenFind();
        shell.Reader.SelectedSidebarTabIndex = 4;
        Dispatcher.UIThread.RunJobs();

        List<string> ids = AutomationIds(view);
        foreach (string expected in new[]
        {
            "Reader.Find.Box", "Reader.Find.Next", "Reader.Find.Previous", "Reader.Find.Count",
            "Reader.History.Back", "Reader.History.Forward", "Reader.Contents.Tab", "Reader.Contents.Empty",
        })
        {
            Assert.Contains(expected, ids);
        }

        Assert.False(shell.Reader.CanHistoryBack);
        window.Close();
        shell.Dispose();
    }

    private static async Task<(ReaderViewModel Reader, FakeReaderSessionService Sessions)> OpenFindReaderAsync()
    {
        var sessions = new FakeReaderSessionService { CurrentRenderer = new OutlineRenderer() };
        ReaderViewModel reader = NewFindReader(sessions, new CaravanSearch());
        Assert.True(await reader.OpenAsync(FindBookId, 0, CancellationToken.None));
        return (reader, sessions);
    }

    private static ReaderViewModel NewFindReader(FakeReaderSessionService sessions, IInDocumentSearchService? search) =>
        new(
            sessions,
            new EmptyAnnotationService(),
            new EmptyBookmarkService(),
            new EmptyLayerService(),
            new EmptyCitationService(),
            new EmptyReadingMemoryService(),
            new InMemoryLocalizationService(),
            search: search);

    private sealed class OutlineRenderer : IPdfRenderer
    {
        public int PageCount => 12;

        public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
            Task.FromException<RenderResult>(new InvalidOperationException("not rendered in this test"));

        public int GetPageRotationDegrees(int pageIndex) => 0;

        public TextLayer ExtractTextLayer(int pageIndex) => new(pageIndex, [], ExtractionQuality.Full);

        public IReadOnlyList<PdfOutlineEntry> ReadOutline() =>
        [
            new("Introduction", 0, 0),
            new("Trade routes", 8, 0),
        ];

        public void Dispose()
        {
        }
    }

    /// <summary>A fake text layer: "Caravan" appears twice on page 2 and once on page 5.</summary>
    private sealed class CaravanSearch : IInDocumentSearchService
    {
        public Task<IReadOnlyList<SearchMatch>> SearchAsync(string bookId, string query, CancellationToken ct)
        {
            var pages = new Dictionary<int, TextWord[]>
            {
                [2] = [new("Caravan", 0.1, 0.1, 0.3, 0.12), new("caravans", 0.1, 0.4, 0.3, 0.42), new("salt", 0.5, 0.5, 0.6, 0.52)],
                [5] = [new("Caravan", 0.2, 0.2, 0.4, 0.22)],
            };
            var matches = new List<SearchMatch>();
            foreach ((int page, TextWord[] words) in pages)
            {
                TextWord[] hits = words
                    .Where(w => w.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (hits.Length > 0)
                {
                    matches.Add(new SearchMatch(
                        page,
                        string.Join(' ', hits.Select(h => h.Text)),
                        hits.Select(h => new HighlightRect(h.Left, h.Top, h.Right, h.Bottom)).ToArray()));
                }
            }

            return Task.FromResult<IReadOnlyList<SearchMatch>>(matches);
        }
    }
}
