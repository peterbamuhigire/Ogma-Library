using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Threading;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.App.ViewModels;

/// <summary>
/// Owns the initial, ready and recoverable degraded states of the desktop shell.
/// </summary>
public sealed class StartupShellViewModel : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IApplicationStartupCoordinator? _coordinator;
    private readonly OgmaRuntimeOptions? _options;
    private readonly ILocalizationService _localization;
    private readonly CatalogueMigrator? _catalogueMigrator;
    private CancellationToken _applicationCancellationToken;
    private ApplicationStartupReport? _report;
    private bool _isStarting = true;
    private bool _isLoadingVisible;
    private bool _isLibraryVisible;
    private bool _isDegraded;
    private bool _canRetry;
    private bool _canExportDiagnostics;
    private string? _exportStatus;
    private string _migrationProgressText = string.Empty;
    private double _migrationProgress;

    /// <summary>Initializes the runtime startup shell.</summary>
    public StartupShellViewModel(
        IApplicationStartupCoordinator coordinator,
        MainShellViewModel mainShell,
        OgmaRuntimeOptions options,
        ILocalizationService localization,
        CatalogueMigrator? catalogueMigrator = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        MainShell = mainShell ?? throw new ArgumentNullException(nameof(mainShell));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _catalogueMigrator = catalogueMigrator;
        if (_catalogueMigrator is not null)
        {
            _catalogueMigrator.ProgressChanged += OnCatalogueMigrationProgress;
        }
        _localization.CultureChanged += OnCultureChanged;
    }

    private StartupShellViewModel(
        ILocalizationService localization,
        string? safeMessage,
        bool isBootstrap)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ConfigurationFailureMessage = safeMessage;
        _isStarting = isBootstrap;
        _isLoadingVisible = isBootstrap;
        _isDegraded = !isBootstrap;
        _canRetry = false;
        _canExportDiagnostics = false;
    }

    /// <summary>Creates the lightweight shell shown before dependency composition.</summary>
    public static StartupShellViewModel CreateBootstrap(ILocalizationService localization) =>
        new(localization, safeMessage: null, isBootstrap: true);

    /// <summary>Creates a fail-closed shell for a redacted configuration failure.</summary>
    public static StartupShellViewModel CreateConfigurationFailure(
        ILocalizationService localization,
        string safeMessage) => new(localization, safeMessage, isBootstrap: false);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The normal catalogue shell, absent when composition itself failed.</summary>
    public MainShellViewModel? MainShell { get; }

    /// <summary>Safe configuration failure copy, when composition could not be built.</summary>
    public string? ConfigurationFailureMessage { get; }

    /// <summary>Window title.</summary>
    public string Title => _localization["MainWindow.Title"];

    /// <summary>Whether startup work is still running.</summary>
    public bool IsStarting
    {
        get => _isStarting;
        private set => SetField(ref _isStarting, value);
    }

    /// <summary>Whether the delayed loading card should be shown.</summary>
    public bool IsLoadingVisible
    {
        get => _isLoadingVisible;
        private set => SetField(ref _isLoadingVisible, value);
    }

    /// <summary>Whether catalogue content is safe to display.</summary>
    public bool IsLibraryVisible
    {
        get => _isLibraryVisible;
        private set => SetField(ref _isLibraryVisible, value);
    }

    /// <summary>Whether the persistent recovery panel should be shown.</summary>
    public bool IsDegraded
    {
        get => _isDegraded;
        private set => SetField(ref _isDegraded, value);
    }

    /// <summary>Whether startup retry can be activated.</summary>
    public bool CanRetry
    {
        get => _canRetry;
        private set => SetField(ref _canRetry, value);
    }

    /// <summary>Whether a redacted diagnostic record is available to export.</summary>
    public bool CanExportDiagnostics
    {
        get => _canExportDiagnostics;
        private set => SetField(ref _canExportDiagnostics, value);
    }

    /// <summary>Safe failure records shown in the recovery panel.</summary>
    public IReadOnlyList<StartupTaskReport> Issues
    {
        get
        {
            if (_report is not null)
            {
                var issues = _report.Failures.ToList();
                issues.AddRange(_report.Capabilities
                    .Where(capability =>
                        capability.Availability == CapabilityAvailability.Unavailable)
                    .Select(capability => new StartupTaskReport(
                        capability.Name,
                        StartupTaskCriticality.Optional,
                        Succeeded: false,
                        capability.Code,
                        capability.Summary,
                        TimeSpan.Zero)));
                return issues;
            }

            return ConfigurationFailureMessage is null
                ? []
                :
                [
                    new StartupTaskReport(
                        "configuration",
                        StartupTaskCriticality.Required,
                        false,
                        "configuration_unusable",
                        ConfigurationFailureMessage,
                        TimeSpan.Zero),
                ];
        }
    }

    /// <summary>Heading for the loading state.</summary>
    public string StartingHeading => _localization["Startup.Starting.Heading"];

    /// <summary>Body for the loading state.</summary>
    public string StartingBody => _localization["Startup.Starting.Body"];

    /// <summary>Redacted canonical migration progress text.</summary>
    public string MigrationProgressText
    {
        get => _migrationProgressText;
        private set => SetField(ref _migrationProgressText, value);
    }

    /// <summary>Canonical migration progress in [0, 1].</summary>
    public double MigrationProgress
    {
        get => _migrationProgress;
        private set => SetField(ref _migrationProgress, value);
    }

    /// <summary>Whether migration has reported a determinate total.</summary>
    public bool IsMigrationProgressKnown => MigrationProgressText.Length > 0;

    /// <summary>Heading for the degraded state.</summary>
    public string DegradedHeading => IsLibraryVisible
        ? _localization["Startup.Degraded.PartialHeading"]
        : _localization["Startup.Degraded.BlockedHeading"];

    /// <summary>Body for the degraded state.</summary>
    public string DegradedBody => IsLibraryVisible
        ? _localization["Startup.Degraded.PartialBody"]
        : _localization["Startup.Degraded.BlockedBody"];

    /// <summary>Retry action label.</summary>
    public string RetryText => _localization["Startup.Action.Retry"];

    /// <summary>Diagnostic export action label.</summary>
    public string ExportDiagnosticsText => _localization["Startup.Action.ExportDiagnostics"];

    /// <summary>Last safe export outcome.</summary>
    public string? ExportStatus
    {
        get => _exportStatus;
        private set => SetField(ref _exportStatus, value);
    }

    /// <summary>Starts the asynchronous application lifecycle.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_coordinator is null || MainShell is null)
        {
            return;
        }

        _applicationCancellationToken = cancellationToken;
        IsStarting = true;
        IsLoadingVisible = false;
        IsDegraded = false;
        CanRetry = false;
        CanExportDiagnostics = false;
        ExportStatus = null;
        MigrationProgressText = string.Empty;
        MigrationProgress = 0;

        Task loadingDelay = RevealLoadingAfterDelayAsync(cancellationToken);
        ApplicationStartupReport report = await _coordinator.InitializeAsync(cancellationToken)
            .ConfigureAwait(true);
        _report = report;

        if (report.CanOpenCatalogue)
        {
            await MainShell.InitializeAsync(cancellationToken).ConfigureAwait(true);
            IsLibraryVisible = true;
        }

        IsStarting = false;
        IsLoadingVisible = false;
        IsDegraded = report.IsDegraded ||
                     report.Capabilities.Any(capability =>
                         capability.Availability == CapabilityAvailability.Unavailable);
        CanRetry = IsDegraded;
        CanExportDiagnostics = true;
        RaiseStateTextChanged();

        try
        {
            await loadingDelay.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    /// <summary>Retries failed or unavailable startup work.</summary>
    public Task RetryAsync() => StartAsync(_applicationCancellationToken);

    /// <summary>Exports redacted startup evidence without exception details or configured paths.</summary>
    public async Task<string?> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        if (_report is null || _options is null)
        {
            return null;
        }

        try
        {
            string directory = Path.Combine(_options.DataDirectory, "diagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"startup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            var document = new
            {
                schemaVersion = 1,
                _report.StartedUtc,
                _report.CompletedUtc,
                _report.CanOpenCatalogue,
                tasks = _report.Tasks.Select(task => new
                {
                    task.Name,
                    criticality = task.Criticality.ToString(),
                    task.Succeeded,
                    task.Code,
                    task.Message,
                    durationMs = Math.Round(task.Duration.TotalMilliseconds, 3),
                }),
                capabilities = _report.Capabilities,
            };
            using FileStream stream = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(
                stream,
                document,
                DiagnosticJsonOptions,
                cancellationToken).ConfigureAwait(true);
            ExportStatus = _localization["Startup.Export.Succeeded"];
            return path;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ExportStatus = _localization["Startup.Export.Failed"];
            return null;
        }
    }

    private async Task RevealLoadingAfterDelayAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(true);
        if (IsStarting)
        {
            IsLoadingVisible = true;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StartingHeading));
        OnPropertyChanged(nameof(StartingBody));
        OnPropertyChanged(nameof(RetryText));
        OnPropertyChanged(nameof(ExportDiagnosticsText));
        RaiseStateTextChanged();
    }

    private void RaiseStateTextChanged()
    {
        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(DegradedHeading));
        OnPropertyChanged(nameof(DegradedBody));
    }

    private void OnCatalogueMigrationProgress(
        object? sender,
        CatalogueMigrationProgress progress)
    {
        void Apply()
        {
            MigrationProgress = progress.TotalItems <= 0
                ? 0
                : Math.Clamp((double)progress.CompletedItems / progress.TotalItems, 0, 1);
            MigrationProgressText = progress.TotalItems <= 0
                ? "Preparing canonical library identities"
                : $"Preparing canonical library identities: {progress.CompletedItems} / {progress.TotalItems}";
            OnPropertyChanged(nameof(IsMigrationProgressKnown));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
