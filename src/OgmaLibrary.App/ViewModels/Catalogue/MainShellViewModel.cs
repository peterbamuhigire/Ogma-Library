using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.ViewModels.Reader;
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

    private ScanPhase _scanPhase = ScanPhase.Idle;
    private int _filesDiscovered;
    private int _filesCompleted;
    private int _filesFailed;
    private CancellationTokenSource _scanCts = new();
    private bool _isSidebarOpen = true;
    private bool _isFilterPanelOpen;
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
    public MainShellViewModel(
        ILocalizationService localization,
        CatalogueViewModel catalogue,
        BookDetailViewModel bookDetail,
        ShelfSidebarViewModel shelfSidebar,
        ReaderViewModel? reader = null,
        ILibrarySettingsService? settingsService = null,
        IIngestionOrchestrator? orchestrator = null,
        IScanProgressService? scanProgress = null)
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
        _settingsService = settingsService;
        _orchestrator = orchestrator;
        _scanProgress = scanProgress;

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

    /// <summary>A placeholder message shown when the reader is opened (until Phase 08).</summary>
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

    /// <summary>True when the reader placeholder message is shown.</summary>
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

    /// <summary>The status-bar text showing scan state or default ready message.</summary>
    public string StatusText
    {
        get
        {
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
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = ChooseFolderText,
                AllowMultiple = false,
            }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        string path = folders[0].Path.LocalPath;
        await _settingsService.SetLibraryRootAsync(path).ConfigureAwait(true);

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
        });
    }

    /// <summary>Cancels the currently running scan, if any.</summary>
    public void CancelScan() => _scanCts.Cancel();

    /// <summary>Toggles the sidebar open/closed.</summary>
    public void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;

    /// <summary>Toggles the filter panel open/closed.</summary>
    public void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <inheritdoc />
    public void Dispose()
    {
        _scanCts.Dispose();
    }

    private void OnProgressChanged(object? sender, ScanProgressSnapshot snapshot)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _scanPhase = snapshot.Phase;
            _filesDiscovered = snapshot.FilesDiscovered;
            _filesCompleted = snapshot.FilesCompleted;
            _filesFailed = snapshot.FilesFailed;
            RaiseAllChanged();
        });
    }

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EmptyStateHeading));
        OnPropertyChanged(nameof(EmptyStateBody));
        OnPropertyChanged(nameof(ChooseFolderText));
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
        OnPropertyChanged(nameof(SortLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
