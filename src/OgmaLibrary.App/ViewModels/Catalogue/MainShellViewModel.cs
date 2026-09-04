using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The high-level content areas of the shell (Phase 08).
/// </summary>
public enum ShellView
{
    /// <summary>The catalogue browsing view (grid/list/directory).</summary>
    Catalogue = 0,

    /// <summary>The PDF reader view (Phase 08).</summary>
    Reader = 1,

    /// <summary>The V2 split-reader scaffold (Phase 15).</summary>
    SplitView = 2,

    /// <summary>The LAN Host sharing settings surface (Phase 16).</summary>
    SharingSettings = 3,

    /// <summary>The classroom student AI smart-search surface (Phase 18).</summary>
    StudentSmartSearch = 4,

    /// <summary>The local recommendation advisor surface.</summary>
    Advisor = 5,

    /// <summary>The reading-plan advisor surface.</summary>
    ReadingPlan = 6,

    /// <summary>The capability-gated native 3D bookshelf route.</summary>
    Bookshelf3D = 7,
}

/// <summary>
/// Marker interface that exposes the navigation target methods of
/// <see cref="MainShellViewModel"/> without requiring the proxy to reference
/// <see cref="MainShellViewModel"/> directly.
/// </summary>
public interface IShellNavigationTarget :
    IBookDetailNavigationService,
    IReaderNavigationService
{
}

/// <summary>
/// The shell view model for Phase 06. Owns the three-pane layout (sidebar /
/// content / status bar), the view-toggle state, and is the concrete
/// implementation of <see cref="IBookDetailNavigationService"/> and
/// <see cref="IReaderNavigationService"/> so no view holds a cross-view reference.
/// It replaces <see cref="MainWindowViewModel"/> once books are available.
/// </summary>
public sealed class MainShellViewModel :
    INotifyPropertyChanged,
    IShellNavigationTarget,
    IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly ILibrarySettingsService? _settingsService;
    private readonly IIngestionOrchestrator? _orchestrator;
    private readonly ILibraryRootService? _libraryRootService;
    private readonly IScanProgressService? _scanProgress;
    private readonly IDirectPdfOpenService? _directPdfOpenService;
    private readonly IClassroomModeService? _classroomModeService;
    private readonly string _searchIconPath = IconCatalog.GetAvaresPath("ic_search_global") ?? string.Empty;
    private readonly string _indexManagerIconPath = IconCatalog.GetAvaresPath("ic_index_manager") ?? string.Empty;
    private readonly string _studentSmartSearchIconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;
    private readonly string _studentSmartSearchLabel = "AI Smart Search";
    private readonly string _classroomOfflineIconPath = IconCatalog.GetAvaresPath("ic_status_unavailable") ?? string.Empty;

    private ScanPhase _scanPhase = ScanPhase.Idle;
    private int _filesDiscovered;
    private int _filesCompleted;
    private int _filesFailed;
    private CancellationTokenSource _scanCts = new();
    private CancellationTokenSource? _catalogueRefreshCts;
    private bool _isSidebarOpen = true;
    private bool _isFilterPanelOpen;
    private bool _isSearchPanelOpen;
    private bool _isIndexManagerOpen;
    private string? _statusOverride;
    private string? _readerPlaceholderMessage;
    private ShellView _activeView = ShellView.Catalogue;
    private bool _isClassroomClientMode;
    private ClassroomConnectivityStatus _classroomConnectivityStatus = new(
        IsOnline: false,
        UpdatedUtc: DateTimeOffset.MinValue,
        Message: "Not connected");
    private IDisposable? _classroomConnectivitySubscription;

    /// <summary>
    /// Full constructor used at runtime.
    /// </summary>
    /// <param name="localization">The localization service.</param>
    /// <param name="catalogue">The catalogue view model.</param>
    /// <param name="bookDetail">The book-detail view model.</param>
    /// <param name="shelfSidebar">The shelf sidebar view model.</param>
    /// <param name="reader">The reader view model.</param>
    /// <param name="splitView">The Phase 15 split-view scaffold.</param>
    /// <param name="settingsService">The library settings service.</param>
    /// <param name="orchestrator">The ingestion orchestrator.</param>
    /// <param name="scanProgress">The scan progress service.</param>
    /// <param name="directPdfOpenService">The direct single-PDF open service.</param>
    /// <param name="search">The Phase 10 search view model.</param>
    /// <param name="indexManager">The Phase 10 Index Manager view model.</param>
    /// <param name="studentSmartSearch">The Phase 18 student AI smart-search view model.</param>
    /// <param name="hostSharing">The Phase 16 Host sharing control view model.</param>
    /// <param name="classroomModeService">The Phase 17 classroom mode/connectivity service.</param>
    /// <param name="advisor">The recommendation advisor view model.</param>
    /// <param name="readingPlan">The reading-plan view model.</param>
    /// <param name="bookshelf3D">The capability-gated 3D bookshelf view model.</param>
    /// <param name="libraryRootService">The durable library-root identity service.</param>
    public MainShellViewModel(
        ILocalizationService localization,
        CatalogueViewModel catalogue,
        BookDetailViewModel bookDetail,
        ShelfSidebarViewModel shelfSidebar,
        ReaderViewModel? reader = null,
        ILibrarySettingsService? settingsService = null,
        IIngestionOrchestrator? orchestrator = null,
        IScanProgressService? scanProgress = null,
        IDirectPdfOpenService? directPdfOpenService = null,
        SearchViewModel? search = null,
        IndexManagerViewModel? indexManager = null,
        StudentSmartSearchViewModel? studentSmartSearch = null,
        SplitViewViewModel? splitView = null,
        HostSharingViewModel? hostSharing = null,
        IClassroomModeService? classroomModeService = null,
        RecommendationPanelViewModel? advisor = null,
        ReadingPlanViewModel? readingPlan = null,
        Bookshelf3DViewModel? bookshelf3D = null,
        ILibraryRootService? libraryRootService = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(bookDetail);
        ArgumentNullException.ThrowIfNull(shelfSidebar);

        _localization = localization;
        Catalogue = catalogue;
        BookDetail = bookDetail;
        ShelfSidebar = shelfSidebar;
        Reader = reader;
        SplitView = splitView;
        Search = search;
        IndexManager = indexManager;
        StudentSmartSearch = studentSmartSearch;
        HostSharing = hostSharing;
        Advisor = advisor;
        ReadingPlan = readingPlan;
        Bookshelf3D = bookshelf3D;
        _settingsService = settingsService;
        _orchestrator = orchestrator;
        _libraryRootService = libraryRootService;
        _scanProgress = scanProgress;
        _directPdfOpenService = directPdfOpenService;
        _classroomModeService = classroomModeService;

        _localization.CultureChanged += (_, _) => RaiseAllChanged();
        Catalogue.PropertyChanged += Catalogue_PropertyChanged;

        if (_scanProgress is not null)
        {
            _scanProgress.ProgressChanged += OnProgressChanged;
        }

        if (HostSharing is not null)
        {
            HostSharing.HostConnectionSucceeded += OnHostConnectionSucceeded;
        }

        if (_classroomModeService is not null)
        {
            _classroomConnectivitySubscription = _classroomModeService.Connectivity.Subscribe(
                new ConnectivityObserver(OnClassroomConnectivityChanged));
            _ = RefreshClassroomConnectivityAsync();
        }
    }

    /// <summary>
    /// Loads the initial catalogue and shelf sidebar so the library is visible on first launch.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the startup load.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Catalogue.LoadAsync(cancellationToken).ConfigureAwait(false);
            await ShelfSidebar.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                SetStatusOverride($"Catalogue load failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Child view models ─────────────────────────────────────────────────────

    /// <summary>The shared catalogue view model (all views bind to this).</summary>
    public CatalogueViewModel Catalogue { get; }

    /// <summary>The book-detail panel view model.</summary>
    public BookDetailViewModel BookDetail { get; }

    /// <summary>The shelf sidebar view model.</summary>
    public ShelfSidebarViewModel ShelfSidebar { get; }

    /// <summary>The reader surface view model.</summary>
    public ReaderViewModel? Reader { get; }

    /// <summary>The split-view scaffold view model.</summary>
    public SplitViewViewModel? SplitView { get; }

    /// <summary>The global search panel view model.</summary>
    public SearchViewModel? Search { get; }

    /// <summary>The Index Manager panel view model.</summary>
    public IndexManagerViewModel? IndexManager { get; }

    /// <summary>The Phase 18 classroom student smart-search view model.</summary>
    public StudentSmartSearchViewModel? StudentSmartSearch { get; }

    /// <summary>The recommendation advisor view model.</summary>
    public RecommendationPanelViewModel? Advisor { get; }

    /// <summary>The reading-plan view model.</summary>
    public ReadingPlanViewModel? ReadingPlan { get; }

    /// <summary>The capability-gated 3D bookshelf view model.</summary>
    public Bookshelf3DViewModel? Bookshelf3D { get; }

    /// <summary>The Phase 16 Host sharing control strip view model.</summary>
    public HostSharingViewModel? HostSharing { get; }

    /// <summary>True when the Host sharing control strip is available.</summary>
    public bool IsHostSharingVisible => HostSharing is not null;

    /// <summary>True when the student smart-search route is available.</summary>
    public bool IsStudentSmartSearchVisible => StudentSmartSearch is not null;

    /// <summary>True when Client mode is disconnected and the shell should show the offline chip.</summary>
    public bool IsClassroomOfflineVisible => _isClassroomClientMode && !_classroomConnectivityStatus.IsOnline;

    /// <summary>Text shown in the Client-mode offline chip.</summary>
    public string ClassroomOfflineText =>
        OfflineText(_classroomConnectivityStatus.Message);

    /// <summary>Accessible label for the Client-mode offline chip.</summary>
    public string ClassroomOfflineAutomationName => $"Classroom connection: {ClassroomOfflineText}";

    /// <summary>Icon path shown in the Client-mode offline chip.</summary>
    public string ClassroomOfflineIconPath => _classroomOfflineIconPath;

    // ── Layout state ──────────────────────────────────────────────────────────

    /// <summary>The currently active content area in the shell.</summary>
    public ShellView ActiveView
    {
        get => _activeView;
        set
        {
            if (_activeView != value)
            {
                _activeView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCatalogueActive));
                OnPropertyChanged(nameof(IsReaderActive));
                OnPropertyChanged(nameof(IsSplitViewActive));
                OnPropertyChanged(nameof(IsSharingSettingsActive));
                OnPropertyChanged(nameof(IsStudentSmartSearchActive));
                OnPropertyChanged(nameof(IsAdvisorActive));
                OnPropertyChanged(nameof(IsReadingPlanActive));
                OnPropertyChanged(nameof(IsBookshelf3DActive));
            }
        }
    }

    /// <summary>True when the catalogue view is the active content area.</summary>
    public bool IsCatalogueActive => _activeView == ShellView.Catalogue;

    /// <summary>True when the reader view is the active content area.</summary>
    public bool IsReaderActive => _activeView == ShellView.Reader;

    /// <summary>True when the split-view scaffold is the active content area.</summary>
    public bool IsSplitViewActive => _activeView == ShellView.SplitView;

    /// <summary>True when the Sharing settings surface is the active content area.</summary>
    public bool IsSharingSettingsActive => _activeView == ShellView.SharingSettings;

    /// <summary>True when the student AI smart-search surface is the active content area.</summary>
    public bool IsStudentSmartSearchActive => _activeView == ShellView.StudentSmartSearch;

    /// <summary>True when the recommendation advisor route is active.</summary>
    public bool IsAdvisorActive => _activeView == ShellView.Advisor;

    /// <summary>True when the reading-plan route is active.</summary>
    public bool IsReadingPlanActive => _activeView == ShellView.ReadingPlan;

    /// <summary>True when the 3D bookshelf route is active.</summary>
    public bool IsBookshelf3DActive => _activeView == ShellView.Bookshelf3D;

    /// <summary>Whether the left sidebar (shelves) is open.</summary>
    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen != value)
            {
                _isSidebarOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether the filter panel flyout is open.</summary>
    public bool IsFilterPanelOpen
    {
        get => _isFilterPanelOpen;
        set
        {
            if (_isFilterPanelOpen != value)
            {
                _isFilterPanelOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether the search panel is open.</summary>
    public bool IsSearchPanelOpen
    {
        get => _isSearchPanelOpen;
        set
        {
            if (_isSearchPanelOpen != value)
            {
                _isSearchPanelOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether the Index Manager panel is open.</summary>
    public bool IsIndexManagerOpen
    {
        get => _isIndexManagerOpen;
        set
        {
            if (_isIndexManagerOpen != value)
            {
                _isIndexManagerOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>An optional reader status message shown in the shell.</summary>
    public string? ReaderPlaceholderMessage
    {
        get => _readerPlaceholderMessage;
        private set
        {
            _readerPlaceholderMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReaderPlaceholderVisible));
        }
    }

    /// <summary>True when a reader status message is shown.</summary>
    public bool IsReaderPlaceholderVisible => _readerPlaceholderMessage is not null;

    // ── Localized labels ──────────────────────────────────────────────────────

    /// <summary>The window title.</summary>
    public string Title => _localization["MainWindow.Title"];

    /// <summary>The empty-state heading shown before a library is chosen.</summary>
    public string EmptyStateHeading => _localization["MainWindow.EmptyState.Heading"];

    /// <summary>The empty-state body text.</summary>
    public string EmptyStateBody => _localization["MainWindow.EmptyState.Body"];

    /// <summary>The label of the primary "choose folder" action.</summary>
    public string ChooseFolderText => _localization["MainWindow.Action.ChooseFolder"];

    /// <summary>The label of the direct PDF open action.</summary>
    public string OpenPdfText => _localization["MainWindow.Action.OpenPdf"];

    /// <summary>The status-bar text showing scan state or default ready message.</summary>
    public string StatusText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_statusOverride))
            {
                return _statusOverride;
            }

            if (_scanPhase == ScanPhase.Complete)
            {
                return string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Scan.Status.Scanned"],
                    _filesCompleted);
            }

            if (_scanPhase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets)
            {
                return string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Scan.Progress.Files"],
                    _filesCompleted,
                    _filesDiscovered);
            }

            if (_scanPhase == ScanPhase.PartialFailure)
            {
                return _localization["Scan.Phase.PartialFailure"];
            }

            return _localization["MainWindow.Status.Ready"];
        }
    }

    /// <summary>The current scan phase label (localized).</summary>
    public string ScanPhaseText => _localization[$"Scan.Phase.{_scanPhase}"];

    /// <summary>Whether a scan is currently active.</summary>
    public bool IsScanning =>
        _scanPhase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;

    /// <summary>Progress in [0.0, 1.0].</summary>
    public double ScanProgress =>
        _filesDiscovered == 0 ? 0.0
        : Math.Min(1.0, (_filesCompleted + _filesFailed) / (double)_filesDiscovered);

    /// <summary>Accessible label for the application logo icon.</summary>
    public string AppLogoLabel => _localization["Icon.ic_app_logo.Label"];

    /// <summary>Accessible label for the settings icon/button.</summary>
    public string SettingsLabel => _localization["Icon.ic_settings.Label"];

    /// <summary>Accessible label for the open-folder icon.</summary>
    public string LibFolderLabel => _localization["Icon.ic_lib_folder_open.Label"];

    /// <summary>The cancel button label.</summary>
    public string CancelScanText => _localization["Scan.Button.Cancel"];

    /// <summary>Grid view button accessible label.</summary>
    public string GridViewLabel => _localization["Icon.ic_cat_view_grid.Label"];

    /// <summary>List view button accessible label.</summary>
    public string ListViewLabel => _localization["Icon.ic_cat_view_list.Label"];

    /// <summary>Directory view button accessible label.</summary>
    public string DirectoryViewLabel => _localization["Icon.ic_cat_view_directory.Label"];

    /// <summary>Catalogue route label.</summary>
    public string LibraryLabel => _localization["Navigation.Library"];

    /// <summary>Reader escape-hatch button label.</summary>
    public string BackToLibraryLabel => _localization["Navigation.BackToLibrary"];

    /// <summary>3D shelf view button accessible label (placeholder).</summary>
    public string Shelf3DViewLabel => _localization["Icon.ic_cat_view_shelf3d.Label"];

    /// <summary>Filter panel toggle label.</summary>
    public string FilterLabel => _localization["Icon.ic_cat_filter.Label"];

    /// <summary>Search panel toggle label.</summary>
    public string SearchLabel => _localization["Icon.ic_search.Label"];

    /// <summary>Index Manager panel toggle label.</summary>
    public string IndexManagerLabel => _localization["IndexManager.Panel.Label"];

    /// <summary>Split-view scaffold route label.</summary>
    public string SplitViewLabel => _localization["SplitView.Title"];

    /// <summary>Sharing settings route label.</summary>
    public string SharingSettingsLabel => _localization["SharingSettings.Title"];

    /// <summary>Student smart-search route label.</summary>
    public string StudentSmartSearchLabel => _studentSmartSearchLabel;

    /// <summary>Recommendation advisor route label.</summary>
    public string AdvisorLabel => _localization["Navigation.Advisor"];

    /// <summary>Reading-plan route label.</summary>
    public string ReadingPlanLabel => _localization["Navigation.ReadingPlan"];

    /// <summary>3D bookshelf route label.</summary>
    public string Bookshelf3DLabel => _localization["Shelf3D.Title"];

    /// <summary>Whether the 3D bookshelf route has a registered host capability.</summary>
    public bool IsShelf3DAvailable => Bookshelf3D is not null;

    /// <summary>Search panel toggle icon path.</summary>
    public string SearchIconPath => _searchIconPath;

    /// <summary>Index Manager panel toggle icon path.</summary>
    public string IndexManagerIconPath => _indexManagerIconPath;

    /// <summary>Student smart-search route icon path.</summary>
    public string StudentSmartSearchIconPath => _studentSmartSearchIconPath;

    /// <summary>Sort label.</summary>
    public string SortLabel => _localization["Icon.ic_cat_sort.Label"];

    /// <summary>Localized filter-panel heading.</summary>
    public string FiltersText => _localization["Catalogue.Filter.Filters"];

    /// <summary>Localized title-filter watermark.</summary>
    public string FilterTitleWatermark => _localization["Catalogue.Filter.Title"];

    /// <summary>Localized author-filter watermark.</summary>
    public string FilterAuthorWatermark => _localization["Catalogue.Filter.Author"];

    /// <summary>Localized clear-filter action.</summary>
    public string ClearFiltersText => _localization["Catalogue.Filter.ClearAll"];

    /// <summary>Localized count of all items matching the active filter.</summary>
    public string CatalogueCountText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Catalogue.CountFormat"],
        Catalogue.TotalFilteredCount);

    // ── Navigation service implementations ────────────────────────────────────

    /// <inheritdoc />
    public async Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ReaderPlaceholderMessage = null;
        await BookDetail.LoadBookAsync(bookId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        if (Reader is not null)
        {
            // Switch immediately so the user gets reader feedback while the
            // isolated worker opens and warms the selected PDF.
            ActiveView = ShellView.Reader;
            ReaderPlaceholderMessage = null;
            BookDetail.IsVisible = false;
            OnPropertyChanged(nameof(IsReaderActive));
            await Reader.OpenAsync(bookId, pageHint, cancellationToken).ConfigureAwait(false);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveView = ShellView.Reader;
            ReaderPlaceholderMessage = null;
            BookDetail.IsVisible = false;
            OnPropertyChanged(nameof(IsReaderActive));
        });
    }

    /// <summary>Returns to the catalogue browsing surface.</summary>
    public void OpenCatalogue()
    {
        ActiveView = ShellView.Catalogue;
        ReaderPlaceholderMessage = null;
    }

    /// <summary>
    /// Closes the open document (flushing progress and releasing the PDF) and returns
    /// to the catalogue browsing surface.
    /// </summary>
    public async Task ReturnToLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (Reader is not null)
        {
            await Reader.CloseAsync(cancellationToken).ConfigureAwait(true);
        }

        OpenCatalogue();
    }

    /// <summary>Opens the Phase 15 split-view scaffold route.</summary>
    public void OpenSplitViewScaffold()
    {
        ActiveView = ShellView.SplitView;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    /// <summary>Opens the Phase 16 Sharing settings surface.</summary>
    public async Task OpenSharingSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (HostSharing is not null)
        {
            await HostSharing.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        ActiveView = ShellView.SharingSettings;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    // ── Scan / folder actions ─────────────────────────────────────────────────

    /// <summary>Opens the Phase 18 classroom student smart-search route.</summary>
    public void OpenStudentSmartSearch()
    {
        ActiveView = ShellView.StudentSmartSearch;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    /// <summary>Opens the local recommendation advisor route.</summary>
    public void OpenAdvisor()
    {
        ActiveView = ShellView.Advisor;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    /// <summary>Opens the reading-plan route.</summary>
    public void OpenReadingPlan()
    {
        ActiveView = ShellView.ReadingPlan;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    /// <summary>Opens the 3D bookshelf route when its capability is registered.</summary>
    public void OpenBookshelf3D()
    {
        if (Bookshelf3D is null)
        {
            return;
        }

        ActiveView = ShellView.Bookshelf3D;
        ReaderPlaceholderMessage = null;
        BookDetail.IsVisible = false;
    }

    /// <summary>
    /// Opens an OS folder picker, persists the chosen root path, and starts a
    /// background scan. Called from the view code-behind on the Choose Folder button.
    /// </summary>
    /// <param name="topLevel">The top-level Avalonia control used to open the folder picker.</param>
    public async Task ChooseFolderAsync(Avalonia.Controls.TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (_settingsService is null || _orchestrator is null)
        {
            SetStatusOverride(_localization["MainWindow.FolderPicker.NotConfigured"]);
            return;
        }

        if (!topLevel.StorageProvider.CanOpen)
        {
            SetStatusOverride(_localization["MainWindow.FolderPicker.Unavailable"]);
            return;
        }

        string? previousRoot = await _settingsService.GetLibraryRootAsync().ConfigureAwait(true);

        if (topLevel is Avalonia.Controls.Window window)
        {
            window.Activate();
        }

        SetStatusOverride(_localization["MainWindow.FolderPicker.Opening"]);

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = ChooseFolderText,
                    AllowMultiple = false,
                }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatusOverride(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["MainWindow.FolderPicker.FailedFormat"],
                ex.Message));
            return;
        }

        if (folders.Count == 0)
        {
            SetStatusOverride(null);
            return;
        }

        string path = folders[0].Path.LocalPath;
        try
        {
            if (_libraryRootService is not null)
            {
                IReadOnlyList<LibraryRootDescriptor> roots = await _libraryRootService
                    .ListAsync()
                    .ConfigureAwait(true);
                LibraryRootDescriptor? currentRoot = roots.FirstOrDefault(root =>
                    SameRootPath(root.CanonicalLocator, previousRoot));
                if (currentRoot is not null)
                {
                    await _libraryRootService.RelinkAsync(currentRoot.Id, path)
                        .ConfigureAwait(true);
                }
                else
                {
                    await _libraryRootService.EnsureForLegacyPathAsync(path)
                        .ConfigureAwait(true);
                }
            }

            await _settingsService.SetLibraryRootAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatusOverride(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["MainWindow.FolderPicker.FailedFormat"],
                ex.Message));
            return;
        }
        SetStatusOverride(_localization["MainWindow.FolderPicker.ScanStarting"]);

        _scanCts.Cancel();
        _scanCts.Dispose();
        _scanCts = new CancellationTokenSource();

        var cts = _scanCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await _orchestrator.ScanAsync(cts.Token).ConfigureAwait(false);
                // After scan completes, refresh the catalogue.
                await Catalogue.LoadAsync(cts.Token).ConfigureAwait(false);
                await ShelfSidebar.LoadAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Scan was cancelled — normal path.
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    SetStatusOverride(string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        _localization["MainWindow.FolderPicker.ScanFailedFormat"],
                        ex.Message)));
            }
        });
    }

    /// <summary>
    /// Registers a user-selected PDF and opens it in the reader immediately.
    /// </summary>
    /// <param name="topLevel">The top-level Avalonia control used to open the file picker.</param>
    public async Task OpenPdfAsync(Avalonia.Controls.TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (_directPdfOpenService is null)
        {
            SetStatusOverride(_localization["MainWindow.PdfPicker.NotConfigured"]);
            return;
        }

        if (!topLevel.StorageProvider.CanOpen)
        {
            SetStatusOverride(_localization["MainWindow.PdfPicker.Unavailable"]);
            return;
        }

        if (topLevel is Avalonia.Controls.Window window)
        {
            window.Activate();
        }

        SetStatusOverride(_localization["MainWindow.PdfPicker.Opening"]);

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = OpenPdfText,
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(_localization["MainWindow.PdfPicker.PdfFiles"])
                        {
                            Patterns = ["*.pdf"],
                            AppleUniformTypeIdentifiers = ["com.adobe.pdf"],
                            MimeTypes = ["application/pdf"],
                        },
                    ],
                }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatusOverride(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["MainWindow.PdfPicker.FailedFormat"],
                ex.Message));
            return;
        }

        if (files.Count == 0)
        {
            SetStatusOverride(null);
            return;
        }

        string path = files[0].Path.LocalPath;
        await OpenPdfPathAsync(path).ConfigureAwait(true);
    }

    /// <summary>
    /// Registers a PDF path, refreshes catalogue projections, and opens it in the reader.
    /// </summary>
    /// <param name="path">Absolute path to the selected PDF.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task OpenPdfPathAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_directPdfOpenService is null)
        {
            SetStatusOverride(_localization["MainWindow.PdfPicker.NotConfigured"]);
            return;
        }

        SetStatusOverride(_localization["MainWindow.PdfPicker.Registering"]);

        try
        {
            string bookId = await _directPdfOpenService.OpenAsync(path, cancellationToken).ConfigureAwait(true);
            await Catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
            await ShelfSidebar.LoadAsync(cancellationToken).ConfigureAwait(true);
            await OpenReaderAsync(bookId, pageHint: null, cancellationToken).ConfigureAwait(true);
            SetStatusOverride(_localization["MainWindow.PdfPicker.OpenedWithMetadata"]);
        }
        catch (Exception ex)
        {
            SetStatusOverride(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["MainWindow.PdfPicker.FailedFormat"],
                ex.Message));
        }
    }

    /// <summary>Reports that the folder picker could not be reached from the current view.</summary>
    public void ReportChooseFolderUnavailable() =>
        SetStatusOverride(_localization["MainWindow.FolderPicker.Unavailable"]);

    /// <summary>Reports that the PDF picker could not be reached from the current view.</summary>
    public void ReportOpenPdfUnavailable() =>
        SetStatusOverride(_localization["MainWindow.PdfPicker.Unavailable"]);

    /// <summary>Cancels the currently running scan, if any.</summary>
    public void CancelScan() => _scanCts.Cancel();

    /// <summary>Toggles the sidebar open/closed.</summary>
    public void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;

    /// <summary>Toggles the filter panel open/closed.</summary>
    public void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <summary>Toggles the search panel open/closed.</summary>
    public void ToggleSearchPanel() => IsSearchPanelOpen = !IsSearchPanelOpen;

    /// <summary>Toggles the Index Manager panel open/closed.</summary>
    public async Task ToggleIndexManagerAsync(CancellationToken cancellationToken = default)
    {
        IsIndexManagerOpen = !IsIndexManagerOpen;
        if (IsIndexManagerOpen && IndexManager is not null)
        {
            await IndexManager.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Catalogue.PropertyChanged -= Catalogue_PropertyChanged;
        _classroomConnectivitySubscription?.Dispose();

        if (HostSharing is not null)
        {
            HostSharing.HostConnectionSucceeded -= OnHostConnectionSucceeded;
        }

        _catalogueRefreshCts?.Cancel();
        _catalogueRefreshCts?.Dispose();
        _scanCts.Dispose();
        Search?.Dispose();
        IndexManager?.Dispose();
        Advisor?.Dispose();
        ReadingPlan?.Dispose();
        Bookshelf3D?.Dispose();
    }

    private static bool SameRootPath(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            string left = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string right = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void Catalogue_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogueViewModel.TotalFilteredCount) or nameof(CatalogueViewModel.FilteredCount))
        {
            OnPropertyChanged(nameof(CatalogueCountText));
        }
    }

    private void OnProgressChanged(object? sender, ScanProgressSnapshot snapshot)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _statusOverride = null;
            _scanPhase = snapshot.Phase;
            _filesDiscovered = snapshot.FilesDiscovered;
            _filesCompleted = snapshot.FilesCompleted;
            _filesFailed = snapshot.FilesFailed;
            RaiseAllChanged();

            if (snapshot.Phase == ScanPhase.Complete)
            {
                ScheduleCatalogueRefresh();
            }
        });
    }

    private void ScheduleCatalogueRefresh()
    {
        _catalogueRefreshCts?.Cancel();
        _catalogueRefreshCts?.Dispose();
        _catalogueRefreshCts = new CancellationTokenSource();
        CancellationToken token = _catalogueRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), token).ConfigureAwait(false);
                await Catalogue.LoadAsync(token).ConfigureAwait(false);
                await ShelfSidebar.LoadAsync(token).ConfigureAwait(false);
                await BookDetail.RefreshLoadedBookAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A newer refresh superseded this one.
            }
        }, token);
    }

    private void OnHostConnectionSucceeded(object? sender, ClassroomConnectionResult result)
    {
        _ = RefreshClassroomConnectivityAsync(result.Connection is null
            ? null
            : new ClassroomConnectivityStatus(
                IsOnline: true,
                UpdatedUtc: result.Connection.ConnectedUtc,
                Message: result.Connection.Request.DisplayName is { Length: > 0 } displayName
                    ? $"Connected to {displayName}"
                    : "Connected to classroom Host"));
        _ = RefreshCatalogueAfterHostConnectionAsync();
    }

    /// <summary>Refreshes the Client-mode offline chip from the current classroom mode service state.</summary>
    public async Task RefreshClassroomConnectivityAsync(
        ClassroomConnectivityStatus? knownStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (_classroomModeService is null)
        {
            return;
        }

        ClassroomModeSettings mode = await _classroomModeService
            .GetModeAsync(cancellationToken)
            .ConfigureAwait(false);
        ClassroomConnectivityStatus status = knownStatus ?? await _classroomModeService
            .GetConnectivityAsync(cancellationToken)
            .ConfigureAwait(false);

        void Apply() => ApplyClassroomConnectivity(mode.Mode == LibraryRuntimeMode.ConnectToHost, status);

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    private async Task RefreshCatalogueAfterHostConnectionAsync()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveView = ShellView.Catalogue;
            ReaderPlaceholderMessage = null;
            BookDetail.Close();
        });

        try
        {
            await Catalogue.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            await ShelfSidebar.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                SetStatusOverride($"Connected, but catalogue refresh failed: {ex.Message}"));
        }
    }

    private void OnClassroomConnectivityChanged(ClassroomConnectivityStatus status)
    {
        _ = RefreshClassroomConnectivityAsync(status);
    }

    private void ApplyClassroomConnectivity(bool isClientMode, ClassroomConnectivityStatus status)
    {
        _isClassroomClientMode = isClientMode;
        _classroomConnectivityStatus = status;
        OnPropertyChanged(nameof(IsClassroomOfflineVisible));
        OnPropertyChanged(nameof(ClassroomOfflineText));
        OnPropertyChanged(nameof(ClassroomOfflineAutomationName));
        OnPropertyChanged(nameof(ClassroomOfflineIconPath));
    }

    private static string OfflineText(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            string.Equals(message, "Not connected", StringComparison.OrdinalIgnoreCase))
        {
            return "Offline";
        }

        return message.StartsWith("Offline", StringComparison.OrdinalIgnoreCase)
            ? message
            : $"Offline - {message}";
    }

    private void SetStatusOverride(string? value)
    {
        _statusOverride = value;
        OnPropertyChanged(nameof(StatusText));
    }

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EmptyStateHeading));
        OnPropertyChanged(nameof(EmptyStateBody));
        OnPropertyChanged(nameof(ChooseFolderText));
        OnPropertyChanged(nameof(OpenPdfText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ScanPhaseText));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(ScanProgress));
        OnPropertyChanged(nameof(AppLogoLabel));
        OnPropertyChanged(nameof(SettingsLabel));
        OnPropertyChanged(nameof(LibFolderLabel));
        OnPropertyChanged(nameof(CancelScanText));
        OnPropertyChanged(nameof(GridViewLabel));
        OnPropertyChanged(nameof(ListViewLabel));
        OnPropertyChanged(nameof(DirectoryViewLabel));
        OnPropertyChanged(nameof(Shelf3DViewLabel));
        OnPropertyChanged(nameof(FilterLabel));
        OnPropertyChanged(nameof(SearchLabel));
        OnPropertyChanged(nameof(IndexManagerLabel));
        OnPropertyChanged(nameof(SplitViewLabel));
        OnPropertyChanged(nameof(SharingSettingsLabel));
        OnPropertyChanged(nameof(StudentSmartSearchLabel));
        OnPropertyChanged(nameof(AdvisorLabel));
        OnPropertyChanged(nameof(ReadingPlanLabel));
        OnPropertyChanged(nameof(Bookshelf3DLabel));
        OnPropertyChanged(nameof(IsClassroomOfflineVisible));
        OnPropertyChanged(nameof(ClassroomOfflineText));
        OnPropertyChanged(nameof(ClassroomOfflineAutomationName));
        OnPropertyChanged(nameof(ClassroomOfflineIconPath));
        OnPropertyChanged(nameof(SearchIconPath));
        OnPropertyChanged(nameof(IndexManagerIconPath));
        OnPropertyChanged(nameof(StudentSmartSearchIconPath));
        OnPropertyChanged(nameof(SortLabel));
        OnPropertyChanged(nameof(FiltersText));
        OnPropertyChanged(nameof(FilterTitleWatermark));
        OnPropertyChanged(nameof(FilterAuthorWatermark));
        OnPropertyChanged(nameof(ClearFiltersText));
        OnPropertyChanged(nameof(CatalogueCountText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class ConnectivityObserver : IObserver<ClassroomConnectivityStatus>
    {
        private readonly Action<ClassroomConnectivityStatus> _onNext;

        public ConnectivityObserver(Action<ClassroomConnectivityStatus> onNext) =>
            _onNext = onNext;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ClassroomConnectivityStatus value) => _onNext(value);
    }
}
