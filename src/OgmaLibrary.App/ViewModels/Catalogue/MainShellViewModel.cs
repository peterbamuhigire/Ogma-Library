using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
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
    private readonly IScanProgressService? _scanProgress;
    private readonly IDirectPdfOpenService? _directPdfOpenService;
    private readonly string _searchIconPath = IconCatalog.GetAvaresPath("ic_search_global") ?? string.Empty;
    private readonly string _indexManagerIconPath = IconCatalog.GetAvaresPath("ic_index_manager") ?? string.Empty;

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

    /// <summary>
    /// Full constructor used at runtime.
    /// </summary>
    /// <param name="localization">The localization service.</param>
    /// <param name="catalogue">The catalogue view model.</param>
    /// <param name="bookDetail">The book-detail view model.</param>
    /// <param name="shelfSidebar">The shelf sidebar view model.</param>
    /// <param name="reader">The reader view model.</param>
    /// <param name="settingsService">The library settings service.</param>
    /// <param name="orchestrator">The ingestion orchestrator.</param>
    /// <param name="scanProgress">The scan progress service.</param>
    /// <param name="directPdfOpenService">The direct single-PDF open service.</param>
    /// <param name="search">The Phase 10 search view model.</param>
    /// <param name="indexManager">The Phase 10 Index Manager view model.</param>
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
        IndexManagerViewModel? indexManager = null)
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
        Search = search;
        IndexManager = indexManager;
        _settingsService = settingsService;
        _orchestrator = orchestrator;
        _scanProgress = scanProgress;
        _directPdfOpenService = directPdfOpenService;

        _localization.CultureChanged += (_, _) => RaiseAllChanged();

        if (_scanProgress is not null)
        {
            _scanProgress.ProgressChanged += OnProgressChanged;
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

    /// <summary>The global search panel view model.</summary>
    public SearchViewModel? Search { get; }

    /// <summary>The Index Manager panel view model.</summary>
    public IndexManagerViewModel? IndexManager { get; }

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
            }
        }
    }

    /// <summary>True when the catalogue view is the active content area.</summary>
    public bool IsCatalogueActive => _activeView == ShellView.Catalogue;

    /// <summary>True when the reader view is the active content area.</summary>
    public bool IsReaderActive => _activeView == ShellView.Reader;

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

    /// <summary>3D shelf view button accessible label (placeholder).</summary>
    public string Shelf3DViewLabel => _localization["Icon.ic_cat_view_shelf3d.Label"];

    /// <summary>Filter panel toggle label.</summary>
    public string FilterLabel => _localization["Icon.ic_cat_filter.Label"];

    /// <summary>Search panel toggle label.</summary>
    public string SearchLabel => _localization["Icon.ic_search.Label"];

    /// <summary>Index Manager panel toggle label.</summary>
    public string IndexManagerLabel => _localization["IndexManager.Panel.Label"];

    /// <summary>Search panel toggle icon path.</summary>
    public string SearchIconPath => _searchIconPath;

    /// <summary>Index Manager panel toggle icon path.</summary>
    public string IndexManagerIconPath => _indexManagerIconPath;

    /// <summary>Sort label.</summary>
    public string SortLabel => _localization["Icon.ic_cat_sort.Label"];

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

    // ── Scan / folder actions ─────────────────────────────────────────────────

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
        await _settingsService.SetLibraryRootAsync(path).ConfigureAwait(true);
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
        _catalogueRefreshCts?.Cancel();
        _catalogueRefreshCts?.Dispose();
        _scanCts.Dispose();
        Search?.Dispose();
        IndexManager?.Dispose();
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
        OnPropertyChanged(nameof(SearchIconPath));
        OnPropertyChanged(nameof(IndexManagerIconPath));
        OnPropertyChanged(nameof(SortLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
