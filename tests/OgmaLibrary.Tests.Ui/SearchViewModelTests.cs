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
    public async Task SearchViewModel_QueryDebouncesAndOpenSelectedNavigates()
    {
        var search = new StubSemanticSearchService();
        var navigation = new RecordingReaderNavigation();
        using var vm = new SearchViewModel(search, navigation, new InMemoryLocalizationService());

        vm.Query = "ogma";
        await WaitForAsync(() => vm.Results.Count == 1);
        await vm.OpenSelectedAsync();

        Assert.Equal("ogma", search.LastQuery);
        Assert.Contains("Semantic", vm.Results[0].MatchLocations, StringComparison.Ordinal);
        Assert.Contains("High", vm.Results[0].Subtitle, StringComparison.Ordinal);
        Assert.Equal("Semantic search active", vm.SearchModeText);
        Assert.Contains("ic_ai_advisor", vm.SearchModeIconPath, StringComparison.Ordinal);
        Assert.True(vm.Results[0].HasConfidence);
        Assert.Contains("ic_status_available", vm.Results[0].ConfidenceIconPath, StringComparison.Ordinal);
        Assert.Contains(vm.Results[0].MatchBadges, badge => badge.AutomationLabel == "Match location: Semantic");
        Assert.All(vm.Results[0].MatchBadges, badge => Assert.False(string.IsNullOrWhiteSpace(badge.IconPath)));
        Assert.Equal("BOOKSEARCH00000000000001", navigation.OpenedBookId);
        Assert.Equal(3, navigation.OpenedPageHint);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_ProviderUnavailable_ShowsExactFallbackMode()
    {
        var navigation = new RecordingReaderNavigation();
        using var vm = new SearchViewModel(
            new UnavailableSemanticSearchService(),
            navigation,
            new InMemoryLocalizationService());

        vm.Query = "offline";
        await WaitForAsync(() => vm.Results.Count == 1);

        Assert.True(vm.IsSemanticDegraded);
        Assert.Equal("Exact search fallback", vm.SearchModeText);
        Assert.Contains("ic_status_unavailable", vm.SearchModeIconPath, StringComparison.Ordinal);
        Assert.Equal("Exact match", vm.Results[0].MatchLocations);
    }

    [AvaloniaFact]
    public async Task SearchViewModel_StaleResults_DoNotOverwriteLatestQuery()
    {
        var search = new OutOfOrderSemanticSearchService();
        var navigation = new RecordingReaderNavigation();
        using var vm = new SearchViewModel(search, navigation, new InMemoryLocalizationService());

        vm.Query = "slow";
        await WaitForAsync(() => search.Queries.Contains("slow"));

        vm.Query = "fast";
        await WaitForAsync(() => vm.Results.Count == 1 && vm.Results[0].Title == "Fast Result");

        await Task.Delay(350);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fast", vm.Query);
        Assert.Equal("Fast Result", vm.Results[0].Title);
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
    public void SearchBar_CtrlK_Opens()
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
        using var search = new SearchViewModel(new StubSemanticSearchService(), navigation, localization);
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

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.IsSearchPanelOpen);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.IsSearchPanelOpen);
        window.Close();
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
        using var searchVm = new SearchViewModel(new StubSemanticSearchService(), navigation, localization);
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

    private sealed class StubSemanticSearchService : ISemanticSearchService
    {
        public string? LastQuery { get; private set; }

        public Task<SemanticSearchResponse> SearchAsync(
            string queryText,
            int maxResults,
            CancellationToken cancellationToken)
        {
            LastQuery = queryText;
            var response = new SemanticSearchResponse(
                ProviderUnavailable: false,
                UsedExactFallback: false,
                [
                    new SemanticSearchResult(
                    "BOOKSEARCH00000000000001",
                    "Ogma Search",
                    12,
                    SearchChunkSource.Page,
                    "<b>ogma</b> search",
                    0.9f,
                    ExactFallback: false,
                    HybridScore: 0.92,
                    MatchLocations: [MatchLocation.Title, MatchLocation.TextPage, MatchLocation.Semantic],
                    ConfidenceLabel: ConfidenceLabel.High,
                    PageIndex: 3),
                ]);
            return Task.FromResult(response);
        }
    }

    private sealed class OutOfOrderSemanticSearchService : ISemanticSearchService
    {
        public List<string> Queries { get; } = [];

        public async Task<SemanticSearchResponse> SearchAsync(
            string queryText,
            int maxResults,
            CancellationToken cancellationToken)
        {
            Queries.Add(queryText);
            await Task.Delay(queryText == "slow" ? 250 : 10, CancellationToken.None).ConfigureAwait(false);
            string title = queryText == "slow" ? "Slow Result" : "Fast Result";
            return new SemanticSearchResponse(
                ProviderUnavailable: false,
                UsedExactFallback: false,
                [
                    new SemanticSearchResult(
                        $"BOOK-{queryText}",
                        title,
                        ChunkId: null,
                        Source: null,
                        Snippet: string.Empty,
                        SemanticScore: null,
                        ExactFallback: true,
                        MatchLocations: [MatchLocation.Title]),
                ]);
        }
    }

    private sealed class UnavailableSemanticSearchService : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(
            string queryText,
            int maxResults,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SemanticSearchResponse(
                ProviderUnavailable: true,
                UsedExactFallback: true,
                [
                    new SemanticSearchResult(
                        "BOOK-OFFLINE",
                        "Offline Result",
                        ChunkId: null,
                        Source: null,
                        Snippet: string.Empty,
                        SemanticScore: null,
                        ExactFallback: true),
                ]));
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
