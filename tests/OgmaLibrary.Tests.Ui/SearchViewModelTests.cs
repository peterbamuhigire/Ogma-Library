using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.App.Views.Search;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Phase 10 search and Index Manager view-model tests.</summary>
public sealed class SearchViewModelTests
{
    private static string ArtifactsDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "screenshots");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

    [AvaloniaFact]
    public async Task SearchViewModel_QueryDebouncesAndOpenSelectedNavigatesToPage()
    {
        var search = new StubUnifiedSearchService();
        var navigation = new RecordingReaderNavigation();
        List<string> focusedBooks = [];
        using var vm = new SearchViewModel(
            search,
            navigation,
            new InMemoryLocalizationService(),
            (bookId, _) =>
            {
                focusedBooks.Add(bookId);
                return Task.CompletedTask;
            });

        Assert.Equal("Searching titles, authors and full text", vm.SearchModeText);
        vm.Query = "o";
        vm.Query = "og";
        vm.Query = "ogma";
        await WaitForAsync(() => vm.Results.Count == 1);
        await vm.OpenSelectedAsync();

        Assert.Equal(["ogma"], search.Queries);
        SearchResultItem item = vm.Results[0];
        Assert.Equal("Ogma Search, by Ada Author, matched on page 4", item.AutomationName);
        Assert.Equal("Ogma Search, by Ada Author, matched on page 4", item.ToString());
        Assert.Equal("by Ada Author", item.Subtitle);
        Assert.Contains("Page 4", item.MatchLocations, StringComparison.Ordinal);
        Assert.Contains(item.MatchBadges, badge => badge.AutomationLabel == "Match location: Title");
        Assert.All(item.MatchBadges, badge => Assert.False(string.IsNullOrWhiteSpace(badge.IconPath)));
        Assert.Equal("Titles, authors, full text and meaning", vm.SearchModeText);
        Assert.Equal("Open at page 4", vm.OpenSelectedLabel);
        Assert.Equal("1 book found", vm.StatusText);
        Assert.Equal("16 of 17 books have searchable text; 1 need OCR", vm.CoverageText);
        Assert.Equal("BOOKSEARCH00000000000001", navigation.OpenedBookId);
        Assert.Equal(3, navigation.OpenedPageHint);
        Assert.Equal(["BOOKSEARCH00000000000001"], focusedBooks);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_ProviderUnavailable_ShowsHonestKeywordMode()
    {
        using var vm = new SearchViewModel(
            new StubUnifiedSearchService { Semantic = SemanticSearchState.Unavailable },
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService());

        vm.Query = "offline";
        await WaitForAsync(() => vm.Results.Count == 1);

        Assert.True(vm.IsSemanticDegraded);
        Assert.Equal("Titles, authors and full text · Semantic search off", vm.SearchModeText);
        Assert.Contains("Settings", vm.SearchModeToolTip, StringComparison.Ordinal);
        Assert.DoesNotContain("active", vm.SearchModeText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_CoverageGap_OffersLinkToFixBooks()
    {
        // Sept-23 Phase 13 (13.8): books without searchable text link to where they are fixed.
        int reviews = 0;
        using var vm = new SearchViewModel(
            new StubUnifiedSearchService(),
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService(),
            reviewCoverage: () => reviews++);

        Assert.False(vm.CanReviewCoverage);
        await vm.RefreshCoverageAsync();

        Assert.True(vm.CanReviewCoverage);
        Assert.Equal("Show books without searchable text", vm.ReviewCoverageLabel);
        vm.ReviewCoverage();
        Assert.Equal(1, reviews);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_ReviewLink_RequiresAGapAndATarget()
    {
        using var vm = new SearchViewModel(
            new PreparingUnifiedSearchService(),
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService(),
            reviewCoverage: () => { });

        await vm.RefreshCoverageAsync();

        // 13 books: 8 searchable, 3 still being read, 2 need OCR: the link is offered for the 2.
        Assert.True(vm.CanReviewCoverage);

        using var complete = new SearchViewModel(
            new StubUnifiedSearchService(),
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService());
        await complete.RefreshCoverageAsync();

        // No navigation target wired: no link, even with a gap.
        Assert.False(complete.CanReviewCoverage);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_NoMatches_ShowsEmptyStateWithSuggestions()
    {
        using var vm = new SearchViewModel(
            new StubUnifiedSearchService { Empty = true },
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService());

        vm.Query = "zzqxnotaword";
        await WaitForAsync(() => vm.IsEmptyState);

        Assert.Empty(vm.Results);
        Assert.Contains("zzqxnotaword", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Check the spelling", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.HasError);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_StaleResults_DoNotOverwriteLatestQuery()
    {
        var search = new StubUnifiedSearchService { SlowQuery = "slow" };
        using var vm = new SearchViewModel(search, new RecordingReaderNavigation(), new InMemoryLocalizationService());

        vm.Query = "slow";
        await WaitForAsync(() => search.Queries.Contains("slow"));

        vm.Query = "fast";
        await WaitForAsync(() => vm.Results.Count == 1 && vm.Results[0].Title == "Result for fast");

        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fast", vm.Query);
        Assert.Equal("Result for fast", vm.Results[0].Title);
        Assert.False(vm.IsSearching);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_Failure_ShowsErrorAndRetryRecovers()
    {
        var search = new StubUnifiedSearchService { Fail = true };
        using var vm = new SearchViewModel(search, new RecordingReaderNavigation(), new InMemoryLocalizationService());

        vm.Query = "broken";
        await WaitForAsync(() => vm.HasError);

        Assert.Equal("Search could not run. Try again; if it keeps failing, the diagnostics log has the details.", vm.StatusText);
        Assert.False(vm.IsSearching);
        Assert.Empty(vm.Results);

        search.Fail = false;
        await vm.RetryAsync();

        Assert.False(vm.HasError);
        Assert.Single(vm.Results);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_QueryImmediatelyAfterOpen_ReturnsResults_50Times()
    {
        // Sept-23 K41 regression: the first query after opening the destination returned 0.
        for (int attempt = 0; attempt < 50; attempt++)
        {
            using var vm = new SearchViewModel(
                new StubUnifiedSearchService(),
                new RecordingReaderNavigation(),
                new InMemoryLocalizationService());
            vm.Query = "Lantern";
            await vm.SearchNowAsync();

            Assert.Single(vm.Results);
            Assert.False(vm.IsSearching);
        }
    }

    [AvaloniaFact]
    public async Task SearchViewModel_RefreshesOpenQueryWhileBooksArePrepared()
    {
        var search = new PreparingUnifiedSearchService();
        using var vm = new SearchViewModel(search, new RecordingReaderNavigation(), new InMemoryLocalizationService());

        vm.Query = "Namutebi";
        await WaitForAsync(() => vm.IsEmptyState);
        Assert.Contains("still being prepared", vm.CoverageText, StringComparison.Ordinal);

        await WaitForAsync(() => vm.Results.Count == 1);
        Assert.Equal("Algorithms Explained", vm.Results[0].Title);
        Assert.False(vm.IsEmptyState);
        Assert.Equal(2, search.Calls);
    }

    [AvaloniaFact]
    public async Task SearchPanel_KeyboardMovesIntoResultsAndEnterOpensAtPage()
    {
        var navigation = new RecordingReaderNavigation();
        using var vm = new SearchViewModel(new StubUnifiedSearchService(), navigation, new InMemoryLocalizationService());
        var view = new SearchPanelView { DataContext = vm };
        var window = new Window { Width = 1000, Height = 500, Content = view };
        window.Show();
        vm.Query = "ogma";
        await WaitForAsync(() => vm.Results.Count == 1);
        Dispatcher.UIThread.RunJobs();

        TextBox box = view.FindControl<TextBox>("SearchBox") ?? throw new InvalidOperationException("SearchBox");
        ListBox list = view.FindControl<ListBox>("SearchResults") ?? throw new InvalidOperationException("SearchResults");
        box.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        var container = (ListBoxItem)list.ContainerFromIndex(0)!;
        Assert.True(container.IsKeyboardFocusWithin);
        Assert.Equal("Ogma Search, by Ada Author, matched on page 4", Avalonia.Automation.AutomationProperties.GetName(container));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await WaitForAsync(() => navigation.OpenedBookId is not null);
        Assert.Equal(3, navigation.OpenedPageHint);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsKeyboardFocusWithin);
        window.Close();
    }

    [AvaloniaFact]
    public async Task IndexManagerViewModel_LoadAndRebuildExposeStatus()
    {
        var service = new StubIndexManagerService();
        var erasure = new StubEmbeddingErasureService();
        using var vm = new IndexManagerViewModel(service, erasure, new InMemoryLocalizationService());

        await vm.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.TotalBooks);
        Assert.Equal(1, vm.IndexedBooks);
        Assert.Single(vm.Books);

        await vm.RebuildAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, service.RebuildCalls);
        Assert.False(vm.IsRebuilding);
        Assert.Contains("complete", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Size: 256 B", vm.SizeSummary);
        Assert.Equal(3, vm.StaleEmbeddingCount);
        Assert.Equal("Stale embeddings: 3", vm.StaleEmbeddingSummary);
        Assert.Equal("Integrity: healthy", vm.IntegritySummary);
        Assert.True(vm.HasOcrJobs);
        Assert.Equal(1, vm.ActiveOcrJobs);
        Assert.Equal("Active OCR jobs: 1", vm.OcrJobsSummary);
        Assert.Contains(vm.OcrJobs, job => job.StateText == "Running" && job.ProgressText == "2/8 pages (25%)");
        Assert.Contains(vm.OcrJobs, job => job.CanPause && job.CanCancel && !job.CanRetry);
        Assert.True(vm.SmartShelfIndexesHealthy);
        Assert.Contains("Smart shelf query:", vm.SmartShelfQuerySummary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task IndexManagerViewModel_OcrJobControls_CallService()
    {
        var service = new StubIndexManagerService();
        using var vm = new IndexManagerViewModel(
            service,
            new StubEmbeddingErasureService(),
            new InMemoryLocalizationService());

        await vm.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        OcrJobStatusDisplayItem running = Assert.Single(vm.OcrJobs);

        await vm.PauseOcrJobAsync(running);
        await vm.CancelOcrJobAsync(running);
        var failed = running with
        {
            CanPause = false,
            CanRetry = true,
        };
        await vm.RetryOcrJobAsync(failed);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(99, service.PausedJobId);
        Assert.Equal(99, service.CancelledJobId);
        Assert.Equal(99, service.RetriedJobId);
        Assert.Contains("retry", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task IndexManagerViewModel_DoesNotRenderRawOperationalErrors()
    {
        const string sensitive = "C:\\Users\\student\\private.pdf token=secret-value";
        using var vm = new IndexManagerViewModel(
            new UnsafeDiagnosticIndexManagerService(sensitive),
            new StubEmbeddingErasureService(),
            new InMemoryLocalizationService());

        await vm.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(vm.ErrorItems, item => item.Contains("student", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vm.ErrorItems, item => item.Contains("secret-value", StringComparison.Ordinal));
        OcrJobStatusDisplayItem job = Assert.Single(vm.OcrJobs);
        Assert.Equal("Failed", job.ErrorMessage);
        Assert.DoesNotContain("student", job.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void SearchBar_CtrlF_OpensSearchAndCtrlK_OpensPalette()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var navigation = new RecordingReaderNavigation();
        var catalogue = new CatalogueViewModel(readModel, navigation, localization);
        var bookDetail = new BookDetailViewModel(readModel, navigation, localization);
        var shelfSidebar = new ShelfSidebarViewModel(
            readModel,
            writeService,
            localization,
            new CatalogueFilterViewModel());
        using var search = new SearchViewModel(new StubUnifiedSearchService(), navigation, localization);
        using var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            search: search);

        var view = new CatalogueShellView { DataContext = shell };
        var window = new Window
        {
            Width = 900,
            Height = 650,
            Content = view,
        };
        window.Show();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        // Sept-23 Phase 07 (T07.8): Ctrl+F is search in context; Ctrl+K opens the palette.
        window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.IsSearchPanelOpen);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.IsSearchPanelOpen);

        view.Focus();
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.IsCommandPaletteOpen);
        Assert.False(shell.IsSearchPanelOpen);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CommandPalette_FiltersAndExecutesKnownCommands()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var navigation = new RecordingReaderNavigation();
        var catalogue = new CatalogueViewModel(readModel, navigation, localization);
        var bookDetail = new BookDetailViewModel(readModel, navigation, localization);
        var shelfSidebar = new ShelfSidebarViewModel(
            readModel,
            writeService,
            localization,
            new CatalogueFilterViewModel());
        using var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar);

        shell.OpenCommandPalette();
        Assert.True(shell.IsCommandPaletteOpen);
        Assert.Contains(shell.CommandPaletteItems, item => item.Id == "nav.search");

        shell.CommandPaletteQuery = "density";
        Assert.Equal("view.toggle-density", shell.CommandPaletteItems[0].Id);

        shell.CommandPaletteQuery = string.Empty;
        await shell.ExecuteCommandAsync("nav.search");
        Assert.True(shell.IsSearchPanelOpen);
        Assert.False(shell.IsCommandPaletteOpen);
    }

    [AvaloniaFact]
    public async Task IndexManager_RebuildButton_ShowsProgress()
    {
        var service = new SlowIndexManagerService();
        using var vm = new IndexManagerViewModel(
            service,
            new StubEmbeddingErasureService(),
            new InMemoryLocalizationService());
        var view = new OgmaLibrary.App.Views.Search.IndexManagerPanelView { DataContext = vm };
        var window = new Window
        {
            Width = 700,
            Height = 400,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.RequestRebuildConfirmation();
        Dispatcher.UIThread.RunJobs();

        Task rebuild = vm.ConfirmRebuildAsync();
        await WaitForAsync(() => vm.IsRebuilding);
        Dispatcher.UIThread.RunJobs();

        ProgressBar progress = view.FindControl<ProgressBar>("RebuildProgress")
            ?? throw new InvalidOperationException("Rebuild progress bar was not found.");
        Assert.True(progress.IsVisible);
        Assert.True(vm.CanCancelRebuild);

        service.Complete();
        await rebuild;
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsRebuilding);
        window.Close();
    }

    [AvaloniaFact]
    public async Task IndexManager_EmbeddingErasure_RequiresCountdownAndCallsService()
    {
        var erasure = new StubEmbeddingErasureService();
        using var vm = new IndexManagerViewModel(
            new StubIndexManagerService(),
            erasure,
            new InMemoryLocalizationService(),
            TimeSpan.Zero);

        vm.RequestEmbeddingErasureConfirmation();
        await WaitForAsync(() => vm.CanConfirmEmbeddingErasure);

        Assert.True(vm.IsEmbeddingErasureConfirmationOpen);
        Assert.Contains("Ready", vm.EmbeddingErasureCountdownText, StringComparison.OrdinalIgnoreCase);

        await vm.ConfirmEmbeddingErasureAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, erasure.EraseCalls);
        Assert.False(vm.IsEmbeddingErasureConfirmationOpen);
        Assert.False(vm.IsErasingEmbeddings);
        Assert.Contains("12", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("3", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SearchIndexPanels_Pseudolocale_RenderWithoutBlankFrame()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("qps-ploc");
        var navigation = new RecordingReaderNavigation();
        using var searchVm = new SearchViewModel(new StubUnifiedSearchService(), navigation, localization);
        using var indexVm = new IndexManagerViewModel(
            new StubIndexManagerService(),
            new StubEmbeddingErasureService(),
            localization);

        searchVm.Query = "ogma";
        await WaitForAsync(() => searchVm.Results.Count == 1);
        await indexVm.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        var content = new StackPanel
        {
            Children =
            {
                new SearchPanelView { DataContext = searchVm },
                new IndexManagerPanelView { DataContext = indexVm },
            },
        };
        var window = new Window
        {
            Width = 1100,
            Height = 700,
            Content = content,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string screenshotPath = Path.Combine(ArtifactsDir, "search-index-pseudo.png");
        frame!.Save(screenshotPath);

        Assert.True(frame.Size.Width > 100);
        Assert.True(frame.Size.Height > 100);
        Assert.Contains("[!!", searchVm.PlaceholderText, StringComparison.Ordinal);
        Assert.Contains("[!!", indexVm.PanelLabel, StringComparison.Ordinal);
        window.Close();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25, timeout.Token);
        }
    }

    private sealed class StubUnifiedSearchService : IUnifiedSearchService
    {
        public List<string> Queries { get; } = [];

        public SemanticSearchState Semantic { get; init; } = SemanticSearchState.Active;

        public bool Empty { get; init; }

        public bool Fail { get; set; }

        public string? SlowQuery { get; init; }

        public async Task<UnifiedSearchResponse> SearchAsync(string? queryText, int maxResults, CancellationToken cancellationToken)
        {
            string query = queryText ?? string.Empty;
            Queries.Add(query);
            if (query == SlowQuery)
            {
                // Ignores cancellation on purpose: a late response must not be applied.
                await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);
            }

            if (Fail)
            {
                throw new InvalidOperationException("index unavailable");
            }

            var coverage = new SearchIndexCoverage(17, 16, 1, 0);
            ParsedSearchQuery parsed = SearchQueryParser.Parse(query);
            if (Empty)
            {
                return new UnifiedSearchResponse(parsed, [], Semantic, false, coverage, TimeSpan.Zero);
            }

            string title = SlowQuery is null ? "Ogma Search" : "Result for " + query;
            UnifiedSearchResult result = new(
                "BOOKSEARCH00000000000001",
                title,
                "Ada Author",
                0.05,
                [new UnifiedMatch(UnifiedMatchKind.Page, 3), new UnifiedMatch(UnifiedMatchKind.Title)],
                new SearchSnippet("ogma search", [new SearchSnippetSpan(0, 4)]),
                new SearchPageJumpTarget("BOOKSEARCH00000000000001", 12, 3));
            return new UnifiedSearchResponse(parsed, [result], Semantic, false, coverage, TimeSpan.Zero);
        }

        public Task<SearchIndexCoverage> GetCoverageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SearchIndexCoverage(17, 16, 1, 0));
    }

    private sealed class PreparingUnifiedSearchService : IUnifiedSearchService
    {
        public int Calls { get; private set; }

        public Task<UnifiedSearchResponse> SearchAsync(string? queryText, int maxResults, CancellationToken cancellationToken)
        {
            Calls++;
            ParsedSearchQuery parsed = SearchQueryParser.Parse(queryText);
            if (Calls == 1)
            {
                return Task.FromResult(new UnifiedSearchResponse(
                    parsed, [], SemanticSearchState.Unavailable, false, new SearchIndexCoverage(13, 8, 2, 3), TimeSpan.Zero));
            }

            UnifiedSearchResult result = new(
                "BOOKALGO0000000000000001",
                "Algorithms Explained",
                "Grace Namutebi",
                0.03,
                [new UnifiedMatch(UnifiedMatchKind.Author)],
                null,
                null);
            return Task.FromResult(new UnifiedSearchResponse(
                parsed, [result], SemanticSearchState.Unavailable, false, new SearchIndexCoverage(13, 11, 2, 0), TimeSpan.Zero));
        }

        public Task<SearchIndexCoverage> GetCoverageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SearchIndexCoverage(13, 8, 2, 3));
    }

    private sealed class RecordingReaderNavigation : IReaderNavigationService, IBookDetailNavigationService
    {
        public string? OpenedBookId { get; private set; }

        public int? OpenedPageHint { get; private set; }

        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OpenReaderAsync(
            string bookId,
            int? pageHint = null,
            CancellationToken cancellationToken = default)
        {
            OpenedBookId = bookId;
            OpenedPageHint = pageHint;
            return Task.CompletedTask;
        }
    }

    private sealed class StubIndexManagerService : IIndexManagerService
    {
        private readonly EventStream _events = new();

        public int RebuildCalls { get; private set; }

        public long? PausedJobId { get; private set; }

        public long? CancelledJobId { get; private set; }

        public long? RetriedJobId { get; private set; }

        public IObservable<IndexStatusUpdate> Events => _events;

        public Task<IndexManagerStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            IndexManagerStatus status = BuildStatus();
            _events.Publish(new IndexStatusUpdate.StatusChanged(status));
            return Task.FromResult(status);
        }

        public Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken)
        {
            RebuildCalls++;
            _events.Publish(new IndexStatusUpdate.RebuildStarted(DateTimeOffset.UtcNow));
            var result = new IndexRebuildResult(true, 2, 2, 0, 4, true, null);
            _events.Publish(new IndexStatusUpdate.RebuildCompleted(result));
            return Task.FromResult(result);
        }

        public Task PauseOcrJobAsync(long jobId, CancellationToken cancellationToken)
        {
            PausedJobId = jobId;
            _events.Publish(new IndexStatusUpdate.StatusChanged(BuildStatus()));
            return Task.CompletedTask;
        }

        public Task CancelOcrJobAsync(long jobId, CancellationToken cancellationToken)
        {
            CancelledJobId = jobId;
            _events.Publish(new IndexStatusUpdate.StatusChanged(BuildStatus()));
            return Task.CompletedTask;
        }

        public Task RetryOcrJobAsync(long jobId, CancellationToken cancellationToken)
        {
            RetriedJobId = jobId;
            _events.Publish(new IndexStatusUpdate.StatusChanged(BuildStatus()));
            return Task.CompletedTask;
        }

        public static IndexManagerStatus BuildStatus() =>
            new(
                TotalBooks: 2,
                IndexedBooks: 1,
                ExtractingBooks: 0,
                FailedBooks: 0,
                PendingOcrPages: 1,
                FailedExtractionPages: 0,
                SearchChunkCount: 4,
                IndexSizeBytes: 256,
                Integrity: new FtsIntegrityResult(true, null),
                StaleEmbeddingCount: 3,
                Books:
                [
                    new BookIndexStatusItem(
                        "BOOKSEARCH00000000000001",
                        "Ogma Search",
                        SearchBookIndexStatus.Indexed,
                        3,
                        4,
                        0,
                        1),
                ],
                OcrJobs:
                [
                    new OcrJobStatusItem(
                        99,
                        "BOOKSEARCH00000000000001",
                        "Ogma Search",
                        OcrJobState.Running,
                        ProcessedPages: 2,
                        TotalPages: 8,
                        ErrorMessage: null),
                ],
                SmartShelfStats: new SmartShelfQueryStats(
                    LastQueryMilliseconds: 3.25,
                    RequiredIndexesHealthy: true,
                    MissingIndexes: []));
    }

    private sealed class UnsafeDiagnosticIndexManagerService(string sensitive) : IIndexManagerService
    {
        private readonly EventStream _events = new();

        public IObservable<IndexStatusUpdate> Events => _events;

        public Task<IndexManagerStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IndexManagerStatus(
                1,
                0,
                0,
                1,
                0,
                1,
                0,
                0,
                new FtsIntegrityResult(false, sensitive),
                [],
                [new OcrJobStatusItem(1, null, "Fixture", OcrJobState.Failed, 0, 1, sensitive)],
                new SmartShelfQueryStats(-1, false, [])));

        public Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IndexRebuildResult(false, 0, 0, 1, 0, false, sensitive));

        public Task PauseOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RetryOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SlowIndexManagerService : IIndexManagerService
    {
        private readonly EventStream _events = new();
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IObservable<IndexStatusUpdate> Events => _events;

        public Task<IndexManagerStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(StubIndexManagerService.BuildStatus());

        public async Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken)
        {
            _events.Publish(new IndexStatusUpdate.RebuildStarted(DateTimeOffset.UtcNow));
            await _completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var result = new IndexRebuildResult(true, 2, 2, 0, 4, true, null);
            _events.Publish(new IndexStatusUpdate.RebuildCompleted(result));
            return result;
        }

        public Task PauseOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RetryOcrJobAsync(long jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Complete() => _completion.TrySetResult();
    }

    private sealed class StubEmbeddingErasureService : IEmbeddingErasureService
    {
        public int EraseCalls { get; private set; }

        public Task<EmbeddingErasureResult> EraseAllAsync(CancellationToken cancellationToken)
        {
            EraseCalls++;
            return Task.FromResult(new EmbeddingErasureResult(12, 3, DateTimeOffset.UtcNow));
        }
    }

    private sealed class EmptyCatalogueReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class NoOpCatalogueWriteService : ICatalogueWriteService
    {
        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("shelf-001");

        public Task RenameShelfAsync(
            string shelfId,
            string newName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddBookToShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EventStream : IObservable<IndexStatusUpdate>
    {
        private readonly List<IObserver<IndexStatusUpdate>> _observers = [];

        public IDisposable Subscribe(IObserver<IndexStatusUpdate> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        public void Publish(IndexStatusUpdate update)
        {
            foreach (IObserver<IndexStatusUpdate> observer in _observers.ToArray())
            {
                observer.OnNext(update);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<IObserver<IndexStatusUpdate>> _observers;
            private readonly IObserver<IndexStatusUpdate> _observer;

            public Subscription(List<IObserver<IndexStatusUpdate>> observers, IObserver<IndexStatusUpdate> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose() => _observers.Remove(_observer);
        }
    }
}
