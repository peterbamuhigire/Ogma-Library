using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.ViewModels.Shelf3D;
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

    /// <summary>The V2 split-reader workspace (Phase 21).</summary>
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

    /// <summary>The Search destination (Sept-23 Phase 07).</summary>
    Search = 8,

    /// <summary>The Collections destination (Sept-23 Phase 07).</summary>
    Collections = 9,

    /// <summary>The Activity destination (Sept-23 Phase 07).</summary>
    Activity = 10,

    /// <summary>The Settings destination (Sept-23 Phase 07; content arrives in Phase 08).</summary>
    Settings = 11,
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

/// <summary>A bounded, searchable shell command exposed by the command palette.</summary>
public sealed record CommandPaletteItem(string Id, string Label, string Hint)
{
    /// <summary>The accessible name: the label and, when present, its shortcut.</summary>
    public string AutomationName => Hint.Length == 0 ? Label : $"{Label} ({Hint})";
}

/// <summary>
/// The shell view model for Phase 06. Owns the three-pane layout (sidebar /
/// content / status bar), the view-toggle state, and is the concrete
/// implementation of <see cref="IBookDetailNavigationService"/> and
/// <see cref="IReaderNavigationService"/> so no view holds a cross-view reference.
/// It is the only window view model (the legacy MainWindow was removed in Sept-23 Phase 05).
/// </summary>
public sealed partial class MainShellViewModel :
    INotifyPropertyChanged,
    IShellNavigationTarget,
    IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly ILibrarySettingsService? _settingsService;
    private readonly IIngestionOrchestrator? _orchestrator;
    private readonly ILibraryRootService? _libraryRootService;
    private readonly IScanProgressService? _scanProgress;
    private readonly IProcessingProgressService? _processingProgress;
    private readonly IDirectPdfOpenService? _directPdfOpenService;
    private readonly IClassroomModeService? _classroomModeService;
    private readonly IUserPreferencesService? _userPreferencesService;
    private readonly ILogger _logger;
    private readonly string _searchIconPath = IconCatalog.GetAvaresPath("ic_search_global") ?? string.Empty;
    private readonly string _indexManagerIconPath = IconCatalog.GetAvaresPath("ic_index_manager") ?? string.Empty;
    private readonly string _studentSmartSearchIconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;
    private readonly string _classroomOfflineIconPath = IconCatalog.GetAvaresPath("ic_status_unavailable") ?? string.Empty;

    private ScanPhase _scanPhase = ScanPhase.Idle;
    private int _filesDiscovered;
    private int _filesCompleted;
    private int _filesFailed;
    private CancellationTokenSource _scanCts = new();
    private CancellationTokenSource? _catalogueRefreshCts;
    private ProcessingSnapshot _processing = ProcessingSnapshot.Idle;
    private DateTimeOffset _lastProgressiveRefreshUtc = DateTimeOffset.MinValue;
    private bool _progressiveRefreshPending;
    private bool _isCommandPaletteOpen;
    private string _commandPaletteQuery = string.Empty;
    private UserPreferences _userPreferences = new();
    private string? _statusOverride;
    private string? _looseBookFolder;
    private ScanSummary? _lastScanSummary;
    private string? _readerPlaceholderMessage;
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
    /// <param name="splitView">The Phase 21 two-session split reader.</param>
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
    /// <param name="userPreferencesService">The persisted desktop appearance preference service.</param>
    /// <param name="reconciliationReviews">The operator relocation-review workflow.</param>
    /// <param name="logger">Optional logger (Sept-23 Phase 02).</param>
    /// <param name="libraryFolders">The Library folders panel and scan monitor (Sept-23 Phase 05).</param>
    /// <param name="processingProgress">Background task progress, kept apart from scan files (Sept-23 Phase 06).</param>
    /// <param name="capabilities">The capability state (Sept-23 Phase 07); defaults to standalone.</param>
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
        ILibraryRootService? libraryRootService = null,
        IUserPreferencesService? userPreferencesService = null,
        ReconciliationReviewPanelViewModel? reconciliationReviews = null,
        ILogger<MainShellViewModel>? logger = null,
        LibraryFoldersViewModel? libraryFolders = null,
        IProcessingProgressService? processingProgress = null,
        ICapabilityState? capabilities = null)
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
        ReconciliationReviews = reconciliationReviews;
        LibraryFolders = libraryFolders;
        _settingsService = settingsService;
        _orchestrator = orchestrator;
        _libraryRootService = libraryRootService;
        _scanProgress = scanProgress;
        _processingProgress = processingProgress;
        _directPdfOpenService = directPdfOpenService;
        _classroomModeService = classroomModeService;
        _userPreferencesService = userPreferencesService;
        _logger = logger ?? (ILogger)NullLogger.Instance;

        _localization.CultureChanged += (_, _) => RaiseAllChanged();
        Catalogue.PropertyChanged += Catalogue_PropertyChanged;

        if (_scanProgress is not null)
        {
            _scanProgress.ProgressChanged += OnProgressChanged;
        }

        if (_processingProgress is not null)
        {
            _processing = _processingProgress.CurrentSnapshot;
            _processingProgress.ProgressChanged += OnProcessingProgressChanged;
            _processingProgress.NotifyChanged();
        }

        if (LibraryFolders is not null)
        {
            LibraryFolders.CatalogueChanged += OnLibraryFoldersChanged;
            LibraryFolders.PropertyChanged += OnLibraryFoldersPropertyChanged;
            LibraryFolders.Monitor.ScanCompleted += OnScanCompleted;
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

        InitializeNavigation(capabilities);
    }

    /// <summary>
    /// Loads the initial catalogue and shelf sidebar so the library is visible on first launch.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the startup load.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Stay on the caller's (UI) context: the catalogue marshals its own list swap,
            // and the shelf sidebar's state is bound (Sept-23 Phase 02, K72).
            await Catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
            await ShelfSidebar.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (LibraryFolders is not null)
            {
                // Sept-23 Phase 05 (T05.8): folders load now; the incremental startup
                // scan and the watchers start after the first frames, off the UI thread.
                await LibraryFolders.StartAsync(LibraryStartupDelay, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   !OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            // K81: never show raw exception text; the detail goes to the redacted log.
            AppLog.CatalogueLoadFailed(_logger, ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                SetStatusOverride(_localization["Catalogue.LoadFailed"]));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever persisted appearance preferences change.</summary>
    public event EventHandler<UserPreferences>? UserPreferencesChanged;

    // ── Child view models ─────────────────────────────────────────────────────

    /// <summary>The shared catalogue view model (all views bind to this).</summary>
    public CatalogueViewModel Catalogue { get; }

    /// <summary>The book-detail panel view model.</summary>
    public BookDetailViewModel BookDetail { get; }

    /// <summary>The shelf sidebar view model.</summary>
    public ShelfSidebarViewModel ShelfSidebar { get; }

    /// <summary>The reader surface view model.</summary>
    public ReaderViewModel? Reader { get; }

    /// <summary>The two-session split reader view model.</summary>
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

    /// <summary>The operator workflow for ambiguous filesystem relocations.</summary>
    public ReconciliationReviewPanelViewModel? ReconciliationReviews { get; }

    /// <summary>The Library folders panel (Sept-23 Phase 05, D-03).</summary>
    public LibraryFoldersViewModel? LibraryFolders { get; }

    /// <summary>Whether the Library folders panel is available.</summary>
    public bool IsLibraryFoldersVisible => LibraryFolders is not null;

    /// <summary>Delay before the startup scan, so the first window paints first.</summary>
    public TimeSpan LibraryStartupDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The folder of the last loose book opened with Open PDF, offered for adding.</summary>
    public string? LooseBookFolder
    {
        get => _looseBookFolder;
        private set
        {
            if (_looseBookFolder != value)
            {
                _looseBookFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAddLooseFolderVisible));
            }
        }
    }

    /// <summary>Whether to offer "Add this folder to your library" after opening a loose book.</summary>
    public bool IsAddLooseFolderVisible => LibraryFolders is not null && !string.IsNullOrWhiteSpace(_looseBookFolder);

    /// <summary>Label of the add-loose-folder action.</summary>
    public string AddLooseFolderText => _localization["MainWindow.PdfPicker.AddFolder"];

    /// <summary>Label of the rescan action.</summary>
    public string RescanLibraryText => _localization["Library.Folders.RescanAll"];

    /// <summary>The Phase 16 Host sharing control strip view model.</summary>
    public HostSharingViewModel? HostSharing { get; }

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

            var culture = System.Globalization.CultureInfo.CurrentCulture;
            int bookCount = Catalogue.TotalCount;

            if (_scanPhase is ScanPhase.Discovering or ScanPhase.Processing)
            {
                // Clamp so the status can never claim more processed files than discovered (K13).
                int discovered = Math.Max(_filesDiscovered, 0);
                int processed = Math.Clamp(_filesCompleted, 0, discovered);
                return string.Format(culture, _localization["Scan.Progress.ScanningFormat"], processed, discovered);
            }

            if (_processing.IsActive)
            {
                // Sept-23 Phase 06 (T06.6, K13): tasks, not files, with their own counters.
                int total = Math.Max(_processing.Total, 0);
                int done = Math.Clamp(_processing.Done, 0, total);
                return string.Format(culture, _localization["Processing.Status.PreparingFormat"], done, total);
            }

            if (_scanPhase == ScanPhase.GeneratingAssets)
            {
                return _localization["Scan.Progress.PreparingBooks"];
            }

            if (_scanPhase == ScanPhase.Cancelled)
            {
                return _localization["Scan.Status.Cancelled"];
            }

            if (_scanPhase == ScanPhase.Failed)
            {
                return _localization["Scan.Status.Failed"];
            }

            if (_scanPhase == ScanPhase.PartialFailure)
            {
                return _lastScanSummary is { } issues && LibraryFolders is not null
                    ? LibraryFolders.FormatSummary(issues)
                    : _localization["Scan.Phase.PartialFailure"];
            }

            if (bookCount > 0)
            {
                // Jobs parked for a missing capability never keep the status "busy".
                return _processing.WaitingByCapability.ContainsKey(JobCapabilities.SemanticEmbeddings)
                    ? string.Format(culture, _localization["Processing.Status.ReadyWaitingFormat"], bookCount)
                    : string.Format(culture, _localization["MainWindow.Status.LibraryReadyFormat"], bookCount);
            }

            return _localization["MainWindow.Status.Ready"];
        }
    }

    /// <summary>The current scan phase label (localized).</summary>
    public string ScanPhaseText => _localization[$"Scan.Phase.{_scanPhase}"];

    /// <summary>Whether a scan is currently active.</summary>
    public bool IsScanning =>
        _scanPhase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;

    /// <summary>Whether background tasks (covers, metadata, indexing) are queued or running.</summary>
    public bool IsProcessing => _processing.IsActive;

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

    /// <summary>3D shelf view button accessible label.</summary>
    public string Shelf3DViewLabel => _localization["Icon.ic_cat_view_shelf3d.Label"];

    /// <summary>Filter panel toggle label.</summary>
    public string FilterLabel => _localization["Icon.ic_cat_filter.Label"];

    /// <summary>Search panel toggle label.</summary>
    public string SearchLabel => _localization["Icon.ic_search.Label"];

    /// <summary>Index Manager panel toggle label.</summary>
    public string IndexManagerLabel => _localization["IndexManager.Panel.Label"];

    /// <summary>Relocation-review panel toggle label.</summary>
    public string ReconciliationReviewLabel => _localization["Reconciliation.Review.Title"];

    /// <summary>Split-reader route label.</summary>
    public string SplitViewLabel => _localization["SplitView.Title"];

    /// <summary>Label of the overflow menu holding rarely used routes (interim, Phase 07 replaces it).</summary>
    public string MoreActionsLabel => _localization["MainWindow.Action.More"];

    /// <summary>Sharing settings route label.</summary>
    public string SharingSettingsLabel => _localization["SharingSettings.Title"];

    /// <summary>Student smart-search route label.</summary>
    public string StudentSmartSearchLabel => _localization["Classroom.Tab.SmartSearch"];

    /// <summary>Recommendation advisor route label.</summary>
    public string AdvisorLabel => _localization["Navigation.Advisor"];

    /// <summary>Reading-plan route label.</summary>
    public string ReadingPlanLabel => _localization["Navigation.ReadingPlan"];

    /// <summary>3D bookshelf route label.</summary>
    public string Bookshelf3DLabel => _localization["Shelf3D.Title"];

    /// <summary>Search panel toggle icon path.</summary>
    public string SearchIconPath => _searchIconPath;

    /// <summary>Index Manager panel toggle icon path.</summary>
    public string IndexManagerIconPath => _indexManagerIconPath;

    /// <summary>Student smart-search route icon path.</summary>
    public string StudentSmartSearchIconPath => _studentSmartSearchIconPath;

    /// <summary>Sort label.</summary>
    public string SortLabel => _localization["Icon.ic_cat_sort.Label"];

    /// <summary>Localized sidebar toggle accessibility label.</summary>
    public string SidebarToggleLabel => _localization["Catalogue.Shell.ToggleSidebar"];

    /// <summary>Localized filter-panel heading.</summary>
    public string FiltersText => _localization["Catalogue.Filter.Filters"];

    /// <summary>Localized title-filter watermark.</summary>
    public string FilterTitleWatermark => _localization["Catalogue.Filter.Title"];

    /// <summary>Localized author-filter watermark.</summary>
    public string FilterAuthorWatermark => _localization["Catalogue.Filter.Author"];

    /// <summary>Localized clear-filter action.</summary>
    public string ClearFiltersText => _localization["Catalogue.Filter.ClearAll"];

    /// <summary>Heading shown when filters hide every book.</summary>
    public string FilteredEmptyHeading => _localization["Catalogue.FilteredEmpty.Heading"];

    /// <summary>Localized count of all items matching the active filter.</summary>
    public string CatalogueCountText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Catalogue.CountFormat"],
        Catalogue.TotalFilteredCount);

    /// <summary>Command palette heading.</summary>
    public string CommandPaletteTitle => _localization["CommandPalette.Title"];

    /// <summary>Command palette query watermark.</summary>
    public string CommandPaletteWatermark => _localization["CommandPalette.Watermark"];

    /// <summary>Current persisted theme choice.</summary>
    public UserTheme Theme => _userPreferences.Theme;

    /// <summary>Current persisted density choice.</summary>
    public UserDensity Density => _userPreferences.Density;

    /// <summary>Loads appearance preferences before the ready shell is shown.</summary>
    public async Task InitializePreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (_userPreferencesService is null)
        {
            return;
        }

        _userPreferences = await _userPreferencesService.GetAsync(cancellationToken)
            .ConfigureAwait(true);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Density));
        UserPreferencesChanged?.Invoke(this, _userPreferences);
    }

    /// <summary>Cycles Light, Dark, and System theme choices.</summary>
    public Task ToggleThemeAsync(CancellationToken cancellationToken = default) =>
        SavePreferencesAsync(_userPreferences with
        {
            Theme = _userPreferences.Theme switch
            {
                UserTheme.Light => UserTheme.Dark,
                UserTheme.Dark => UserTheme.System,
                _ => UserTheme.Light,
            },
        }, cancellationToken);

    /// <summary>Toggles between comfortable and compact density.</summary>
    public Task ToggleDensityAsync(CancellationToken cancellationToken = default) =>
        SavePreferencesAsync(_userPreferences with
        {
            Density = _userPreferences.Density == UserDensity.Comfortable
                ? UserDensity.Compact
                : UserDensity.Comfortable,
        }, cancellationToken);

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

        void Show()
        {
            NavigateTo(NavigationRoute.Reader(bookId, pageHint));
            BookDetail.IsVisible = false;
            OnPropertyChanged(nameof(IsReaderContentVisible));
            OnPropertyChanged(nameof(IsReadingEmptyVisible));
        }

        if (Reader is not null)
        {
            // Switch immediately so the user gets reader feedback while the
            // isolated worker opens and warms the selected PDF.
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Show();
            }

            await Reader.OpenAsync(bookId, pageHint, cancellationToken).ConfigureAwait(false);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(Show);
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

        if (topLevel is Avalonia.Controls.Window window)
        {
            window.Activate();
        }

        SetStatusOverride(_localization["MainWindow.FolderPicker.Opening"]);

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await PickLibraryFoldersAsync(topLevel.StorageProvider).ConfigureAwait(true);
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "catalogue.choose_folder");
            SetStatusOverride(_localization["MainWindow.FolderPicker.Failed"]);
            return;
        }

        if (folders.Count == 0)
        {
            SetStatusOverride(null);
            return;
        }

        string path = folders[0].Path.LocalPath;
        await AddLibraryFolderAsync(path).ConfigureAwait(true);
    }

    /// <summary>
    /// Adds a folder to the library and scans it (Sept-23 Phase 05, T05.3). Choosing a
    /// folder never relinks or hides an existing folder; moving a folder is the
    /// separate, explicit relink action in the Library folders panel.
    /// </summary>
    /// <param name="path">The absolute folder path.</param>
    public async Task AddLibraryFolderAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (LibraryFolders is not null)
        {
            try
            {
                SetStatusOverride(_localization["MainWindow.FolderPicker.ScanStarting"]);
                await LibraryFolders.AddFolderAsync(path).ConfigureAwait(true);
                if (LibraryFolders.StatusText is { Length: > 0 } status)
                {
                    SetStatusOverride(status);
                }
            }
            catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
            {
                AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "catalogue.add_folder");
                SetStatusOverride(_localization["MainWindow.FolderPicker.Failed"]);
            }

            return;
        }

        if (_settingsService is null || _orchestrator is null)
        {
            SetStatusOverride(_localization["MainWindow.FolderPicker.NotConfigured"]);
            return;
        }

        try
        {
            if (_libraryRootService is not null)
            {
                IReadOnlyList<LibraryRootDescriptor> roots = await _libraryRootService
                    .ListAsync()
                    .ConfigureAwait(true);
                if (!roots.Any(root => SameRootPath(root.CanonicalLocator, path)))
                {
                    await _libraryRootService.AddAsync(path).ConfigureAwait(true);
                }
            }
            else
            {
                await _settingsService.SetLibraryRootAsync(path).ConfigureAwait(true);
            }
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "catalogue.choose_folder");
            SetStatusOverride(_localization["MainWindow.FolderPicker.Failed"]);
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
            catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
            {
                AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "scan.run");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    SetStatusOverride(_localization["MainWindow.FolderPicker.ScanFailed"]));
            }
        });
    }

    private Task<IReadOnlyList<IStorageFolder>> PickLibraryFoldersAsync(IStorageProvider storageProvider)
    {
#if OGMA_E2E
        // Sept-23 T01.5 test hook. Compiled only into E2E builds (-p:OgmaE2EHooks=true), never into
        // shipped configurations (guarded by an architecture test). The native folder dialog exposes
        // no Select Folder button to UI Automation, so journeys may pass the folder through
        // OGMA_E2E_PICK_FOLDER; everything after the picker runs the production path unchanged.
        string? seededFolder = Environment.GetEnvironmentVariable("OGMA_E2E_PICK_FOLDER");
        if (!string.IsNullOrWhiteSpace(seededFolder))
        {
            return PickSeededFolderForE2EAsync(storageProvider, seededFolder);
        }
#endif
        return storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = ChooseFolderText,
                AllowMultiple = false,
            });
    }

#if OGMA_E2E
    private static async Task<IReadOnlyList<IStorageFolder>> PickSeededFolderForE2EAsync(
        IStorageProvider storageProvider,
        string path)
    {
        IStorageFolder? folder = await storageProvider.TryGetFolderFromPathAsync(path).ConfigureAwait(true);
        return folder is null ? [] : [folder];
    }
#endif

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
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "catalogue.open_pdf_picker");
            SetStatusOverride(_localization["MainWindow.PdfPicker.Failed"]);
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
            bool isLoose = await IsLooseFileAsync(path, cancellationToken).ConfigureAwait(true);
            LooseBookFolder = isLoose ? Path.GetDirectoryName(Path.GetFullPath(path)) : null;
            SetStatusOverride(isLoose
                ? _localization["MainWindow.PdfPicker.OpenedLoose"]
                : _localization["MainWindow.PdfPicker.OpenedWithMetadata"]);
        }
        catch (InvalidPdfFileException invalid)
        {
            SetStatusOverride(invalid.Validity == FileValidity.Empty
                ? _localization["MainWindow.PdfPicker.InvalidEmpty"]
                : _localization["MainWindow.PdfPicker.InvalidNotPdf"]);
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "catalogue.open_pdf");
            SetStatusOverride(_localization["MainWindow.PdfPicker.Failed"]);
        }
    }

    /// <summary>Adds the folder of the last loose book to the library (T05.11).</summary>
    public async Task AddLooseFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_looseBookFolder))
        {
            return;
        }

        string folder = _looseBookFolder;
        LooseBookFolder = null;
        await AddLibraryFolderAsync(folder).ConfigureAwait(true);
    }

    /// <summary>Rescans every enabled library folder (T05.8).</summary>
    public async Task RescanLibraryAsync()
    {
        if (LibraryFolders is not null)
        {
            SetStatusOverride(null);
            await LibraryFolders.RescanAsync().ConfigureAwait(true);
            return;
        }

        if (_orchestrator is not null)
        {
            await _orchestrator.ScanRootsAsync(null).ConfigureAwait(true);
        }
    }

    /// <summary>Reports that the folder picker could not be reached from the current view.</summary>
    public void ReportChooseFolderUnavailable() =>
        SetStatusOverride(_localization["MainWindow.FolderPicker.Unavailable"]);

    /// <summary>Reports that the PDF picker could not be reached from the current view.</summary>
    public void ReportOpenPdfUnavailable() =>
        SetStatusOverride(_localization["MainWindow.PdfPicker.Unavailable"]);

    /// <summary>Cancels the currently running scan, if any.</summary>
    public void CancelScan()
    {
        LibraryFolders?.Monitor.CancelScan();
        _scanCts.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Catalogue.PropertyChanged -= Catalogue_PropertyChanged;
        if (_processingProgress is not null)
        {
            _processingProgress.ProgressChanged -= OnProcessingProgressChanged;
        }

        Catalogue.PropertyChanged -= OnCatalogueViewChanged;
        Catalogue.Filter.PropertyChanged -= OnFilterChanged;
        ShelfSidebar.PropertyChanged -= OnShelfSidebarChanged;
        _navigation.Changed -= OnNavigationChanged;
        _capabilities.Changed -= OnCapabilitiesChanged;
        if (Reader is not null)
        {
            Reader.PropertyChanged -= OnReaderPropertyChanged;
        }

        _classroomConnectivitySubscription?.Dispose();
        if (LibraryFolders is not null)
        {
            LibraryFolders.CatalogueChanged -= OnLibraryFoldersChanged;
            LibraryFolders.PropertyChanged -= OnLibraryFoldersPropertyChanged;
            LibraryFolders.Monitor.ScanCompleted -= OnScanCompleted;
        }

        if (HostSharing is not null)
        {
            HostSharing.HostConnectionSucceeded -= OnHostConnectionSucceeded;
            HostSharing.Dispose();
        }

        _catalogueRefreshCts?.Cancel();
        _catalogueRefreshCts?.Dispose();
        _scanCts.Dispose();
        Search?.Dispose();
        IndexManager?.Dispose();
        Advisor?.Dispose();
        ReadingPlan?.Dispose();
        Bookshelf3D?.Dispose();
        ReconciliationReviews?.Dispose();
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

    /// <summary>Search text entered in the command palette.</summary>
    public string CommandPaletteQuery
    {
        get => _commandPaletteQuery;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(_commandPaletteQuery, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _commandPaletteQuery = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CommandPaletteItems));
        }
    }

    /// <summary>Whether the keyboard command palette is visible.</summary>
    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set
        {
            if (_isCommandPaletteOpen != value)
            {
                _isCommandPaletteOpen = value;
                OnPropertyChanged();
            }
        }
    }

    private void Catalogue_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogueViewModel.TotalFilteredCount) or nameof(CatalogueViewModel.FilteredCount)
            or nameof(CatalogueViewModel.TotalCount))
        {
            OnPropertyChanged(nameof(CatalogueCountText));
            OnPropertyChanged(nameof(StatusText));
        }

        if (e.PropertyName is nameof(CatalogueViewModel.TotalPages))
        {
            OnPropertyChanged(nameof(IsPagerVisible));
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

            if (snapshot.Phase is ScanPhase.Complete or ScanPhase.PartialFailure or ScanPhase.Cancelled)
            {
                ScheduleCatalogueRefresh();
            }
        });
    }

    private void OnProcessingProgressChanged(object? sender, ProcessingSnapshot snapshot)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ProcessingSnapshot previous = _processing;
            _processing = snapshot;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsProcessing));

            // T06.6: covers and metadata appear while processing continues, not only when the
            // queue drains; refreshes are throttled to one every two seconds.
            if (snapshot.AssetsCompleted != previous.AssetsCompleted ||
                (previous.IsActive && !snapshot.IsActive))
            {
                RequestProgressiveRefresh();
            }
        });
    }

    private void RequestProgressiveRefresh()
    {
        TimeSpan sinceLast = DateTimeOffset.UtcNow - _lastProgressiveRefreshUtc;
        if (sinceLast >= ProgressiveRefreshInterval)
        {
            _lastProgressiveRefreshUtc = DateTimeOffset.UtcNow;
            ScheduleCatalogueRefresh();
            return;
        }

        if (_progressiveRefreshPending)
        {
            return;
        }

        _progressiveRefreshPending = true;
        Avalonia.Threading.DispatcherTimer.RunOnce(
            () =>
            {
                _progressiveRefreshPending = false;
                _lastProgressiveRefreshUtc = DateTimeOffset.UtcNow;
                ScheduleCatalogueRefresh();
            },
            ProgressiveRefreshInterval - sinceLast);
    }

    private static readonly TimeSpan ProgressiveRefreshInterval = TimeSpan.FromSeconds(2);

    private void OnScanCompleted(object? sender, ScanSummary summary)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _lastScanSummary = summary;
            _statusOverride = null;
            // The monitor's outcome is authoritative even if no progress event arrived
            // (for example a scan cancelled while it waited for another scan).
            _scanPhase = summary.Outcome switch
            {
                ScanOutcome.Cancelled => ScanPhase.Cancelled,
                ScanOutcome.Failed => ScanPhase.Failed,
                ScanOutcome.CompletedWithIssues => ScanPhase.PartialFailure,
                _ => _scanPhase is ScanPhase.GeneratingAssets ? _scanPhase : ScanPhase.Complete,
            };
            RaiseAllChanged();
            ScheduleCatalogueRefresh();
        });
    }

    private void OnLibraryFoldersChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(ScheduleCatalogueRefresh);

    private async Task<bool> IsLooseFileAsync(string path, CancellationToken cancellationToken)
    {
        if (_libraryRootService is null)
        {
            return false;
        }

        IReadOnlyList<LibraryRootDescriptor> roots = await _libraryRootService
            .ListAsync(cancellationToken)
            .ConfigureAwait(true);
        string full = Path.GetFullPath(path);
        return !roots.Any(root =>
            root.IsEnabled &&
            !string.IsNullOrWhiteSpace(root.CanonicalLocator) &&
            full.StartsWith(
                Path.TrimEndingDirectorySeparator(root.CanonicalLocator) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
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
            NavigateTo(NavigationRoute.Library);
            ReaderPlaceholderMessage = null;
            BookDetail.Close();
        });

        try
        {
            await Catalogue.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            await ShelfSidebar.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(MainShellViewModel), "classroom.catalogue_refresh");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                SetStatusOverride(_localization["Catalogue.RefreshAfterConnectFailed"]));
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
        (_capabilities as RuntimeCapabilityState)?.SetClassroomClientConnected(isClientMode && status.IsOnline);
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

    private async Task SavePreferencesAsync(
        UserPreferences preferences,
        CancellationToken cancellationToken)
    {
        if (_userPreferencesService is null)
        {
            return;
        }

        await _userPreferencesService.SaveAsync(preferences, cancellationToken)
            .ConfigureAwait(true);
        _userPreferences = preferences;
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Density));
        OnPropertyChanged(nameof(CommandPaletteItems));
        UserPreferencesChanged?.Invoke(this, _userPreferences);
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
        OnPropertyChanged(nameof(IsProcessing));
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
        OnPropertyChanged(nameof(ReconciliationReviewLabel));
        OnPropertyChanged(nameof(SplitViewLabel));
        OnPropertyChanged(nameof(MoreActionsLabel));
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
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(FiltersText));
        OnPropertyChanged(nameof(FilterTitleWatermark));
        OnPropertyChanged(nameof(FilterAuthorWatermark));
        OnPropertyChanged(nameof(ClearFiltersText));
        OnPropertyChanged(nameof(FilteredEmptyHeading));
        OnPropertyChanged(nameof(CatalogueCountText));
        OnPropertyChanged(nameof(CommandPaletteItems));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Density));
        OnPropertyChanged(nameof(CommandPaletteTitle));
        OnPropertyChanged(nameof(CommandPaletteWatermark));
        OnPropertyChanged(nameof(AddLooseFolderText));
        OnPropertyChanged(nameof(RescanLibraryText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

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
