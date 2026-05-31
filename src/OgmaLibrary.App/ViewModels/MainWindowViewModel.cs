using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.App.ViewModels;

/// <summary>
/// The view model for the main window. All visible text is resolved through
/// <see cref="ILocalizationService"/> so no string is hard-coded in the view, and the
/// bound text updates when the active culture changes (en/fr at MVP).
/// Scan progress is updated live via <see cref="IScanProgressService"/>.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
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

    /// <summary>Creates the view model with all required services.</summary>
    /// <param name="localization">The localization service.</param>
    /// <param name="settingsService">The library settings service.</param>
    /// <param name="orchestrator">The ingestion orchestrator.</param>
    /// <param name="scanProgress">The scan progress service.</param>
    public MainWindowViewModel(
        ILocalizationService localization,
        ILibrarySettingsService settingsService,
        IIngestionOrchestrator orchestrator,
        IScanProgressService scanProgress)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(scanProgress);

        _localization = localization;
        _settingsService = settingsService;
        _orchestrator = orchestrator;
        _scanProgress = scanProgress;

        _localization.CultureChanged += (_, _) => RaiseAllChanged();
        _scanProgress.ProgressChanged += OnProgressChanged;
    }

    /// <summary>A design-time instance so the XAML previewer can render the window.</summary>
    public MainWindowViewModel()
        : this(new DesignLocalizationService())
    {
    }

    private MainWindowViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _localization.CultureChanged += (_, _) => RaiseAllChanged();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The window title.</summary>
    public string Title => _localization["MainWindow.Title"];

    /// <summary>The product tagline shown under the title.</summary>
    public string Tagline => _localization["MainWindow.Tagline"];

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

    /// <summary>Number of files discovered so far.</summary>
    public int FilesDiscovered => _filesDiscovered;

    /// <summary>Number of files completed so far.</summary>
    public int FilesCompleted => _filesCompleted;

    /// <summary>Number of files that failed.</summary>
    public int FilesFailed => _filesFailed;

    /// <summary>Accessible label for the application logo icon.</summary>
    public string AppLogoLabel => _localization["Icon.ic_app_logo.Label"];

    /// <summary>Accessible label for the settings icon/button.</summary>
    public string SettingsLabel => _localization["Icon.ic_settings.Label"];

    /// <summary>Accessible label for the open-folder icon.</summary>
    public string LibFolderLabel => _localization["Icon.ic_lib_folder_open.Label"];

    /// <summary>The cancel button label.</summary>
    public string CancelScanText => _localization["Scan.Button.Cancel"];

    /// <summary>
    /// Opens an OS folder picker, persists the chosen root path, and starts a
    /// background scan. Called from the view code-behind on the Choose Folder button.
    /// </summary>
    /// <param name="topLevel">The top-level Avalonia control used to open the folder picker.</param>
    public async Task ChooseFolderAsync(TopLevel topLevel)
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
            }).ConfigureAwait(true); // Return to UI thread.

        if (folders.Count == 0)
        {
            return;
        }

        string path = folders[0].Path.LocalPath;
        await _settingsService.SetLibraryRootAsync(path).ConfigureAwait(true);

        // Cancel any ongoing scan and start a fresh one.
        _scanCts.Cancel();
        _scanCts.Dispose();
        _scanCts = new CancellationTokenSource();

        var cts = _scanCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await _orchestrator.ScanAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Scan was cancelled — normal path.
            }
        });
    }

    /// <summary>Cancels the currently running scan, if any.</summary>
    public void CancelScan()
    {
        _scanCts.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scanCts.Dispose();
    }

    private void OnProgressChanged(object? sender, ScanProgressSnapshot snapshot)
    {
        // This may be called from a background thread — post to UI thread.
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
        OnPropertyChanged(nameof(Tagline));
        OnPropertyChanged(nameof(EmptyStateHeading));
        OnPropertyChanged(nameof(EmptyStateBody));
        OnPropertyChanged(nameof(ChooseFolderText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ScanPhaseText));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(ScanProgress));
        OnPropertyChanged(nameof(FilesDiscovered));
        OnPropertyChanged(nameof(FilesCompleted));
        OnPropertyChanged(nameof(FilesFailed));
        OnPropertyChanged(nameof(AppLogoLabel));
        OnPropertyChanged(nameof(SettingsLabel));
        OnPropertyChanged(nameof(LibFolderLabel));
        OnPropertyChanged(nameof(CancelScanText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>A minimal English localization used only by the design-time constructor.</summary>
    private sealed class DesignLocalizationService : ILocalizationService
    {
        public System.Globalization.CultureInfo CurrentCulture { get; } =
            System.Globalization.CultureInfo.GetCultureInfo("en");

        public event EventHandler? CultureChanged { add { } remove { } }

        public string this[string key] => key switch
        {
            "MainWindow.Title" => "Ogma Library",
            "MainWindow.Tagline" => "Your personal PDF library.",
            "MainWindow.EmptyState.Heading" => "Your library will appear here",
            "MainWindow.EmptyState.Body" => "Choose a folder of PDFs to begin.",
            "MainWindow.Action.ChooseFolder" => "Choose library folder",
            "MainWindow.Status.Ready" => "Ready — choose a library folder to begin",
            "MainWindow.Status.Skeleton" => "Skeleton build — Phase 02",
            "Scan.Status.Scanned" => "Scanned {0} books",
            "Scan.Phase.Idle" => "Ready",
            "Scan.Phase.Discovering" => "Discovering files…",
            "Scan.Phase.Processing" => "Processing…",
            "Scan.Phase.GeneratingAssets" => "Generating thumbnails…",
            "Scan.Phase.Complete" => "Scan complete",
            "Scan.Phase.PartialFailure" => "Scan complete — some files failed",
            "Scan.Phase.Cancelled" => "Scan cancelled",
            "Scan.Button.Cancel" => "Cancel",
            "Scan.Progress.Files" => "{0} / {1} files",
            "Scan.Progress.Failed" => "{0} failed",
            "Icon.ic_app_logo.Label" => "Ogma Library logo",
            "Icon.ic_settings.Label" => "Settings",
            "Icon.ic_lib_folder_open.Label" => "Open library folder",
            _ => key,
        };

        public void SetCulture(string cultureName)
        {
        }
    }
}
