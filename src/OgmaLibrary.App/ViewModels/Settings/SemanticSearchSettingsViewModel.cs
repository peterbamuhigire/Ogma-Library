using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.Settings;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.App.ViewModels.Settings;

/// <summary>What the built-in model card tells the user.</summary>
public enum SemanticModelState
{
    /// <summary>The status has not been read yet.</summary>
    Unknown = 0,

    /// <summary>No complete model is installed; a download is offered.</summary>
    NotInstalled = 1,

    /// <summary>The model is being downloaded.</summary>
    Downloading = 2,

    /// <summary>Every byte has arrived and the files are being checked.</summary>
    Verifying = 3,

    /// <summary>The model is installed and verified.</summary>
    Ready = 4,

    /// <summary>The install failed for a stated reason; a retry is offered.</summary>
    Error = 5,

    /// <summary>The download server could not be reached; a retry is offered.</summary>
    Offline = 6,
}

/// <summary>
/// Settings → Search and AI (Sept-23 Phase 14, task 14.7; closes K141). The provider choice
/// (Built-in, Ollama, Off), the built-in model's install state with a consented download, the
/// smart index's progress with build, pause and resume, an erase with confirmation and Ollama
/// detection with setup guidance. The model download starts only from the Download button,
/// after the size and the destination are stated (D-104). Every mutation from a background
/// event or a library await is marshalled through <see cref="IUiDispatcher"/>.
/// </summary>
public sealed class SemanticSearchSettingsViewModel : INotifyPropertyChanged, IObserver<SemanticIndexEvent>, IDisposable
{
    private const double BytesPerMegabyte = 1_000_000d;
    private static readonly TimeSpan ProgressRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly ISemanticCapabilityService _capability;
    private readonly IEmbeddingModelInstaller _installer;
    private readonly ISemanticIndexControl _control;
    private readonly ISemanticSearchReadModel _progress;
    private readonly IEmbeddingErasureService _erasure;
    private readonly UserPreferencesController _preferences;
    private readonly ILocalizationService _localization;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger _logger;
    private IDisposable? _subscription;
    private CancellationTokenSource? _download;
    private EmbeddingModelInstallStatus? _status;
    private SemanticModelState _modelState;
    private string _modelStatusKey = string.Empty;
    private double _downloadPercent;
    private SemanticIndexProgress _indexProgress = new(0, 0, 0, null, false);
    private bool _isEraseConfirmationVisible;
    private string _eraseStatusText = string.Empty;
    private int _progressRefreshQueued;
    private Task _pendingWork = Task.CompletedTask;
    private bool _disposed;

    /// <summary>Creates the view model; call <see cref="LoadAsync"/> before showing it.</summary>
    /// <param name="capability">The provider choice and availability.</param>
    /// <param name="installer">Installs, verifies and removes the built-in model.</param>
    /// <param name="control">Pauses, resumes and wakes background indexing.</param>
    /// <param name="progress">Reports smart index progress and lifecycle events.</param>
    /// <param name="erasure">Erases embedding vectors.</param>
    /// <param name="preferences">Persists the provider choice.</param>
    /// <param name="localization">Localized labels.</param>
    /// <param name="dispatcher">Marshals background updates to the UI thread.</param>
    /// <param name="logger">Diagnostics logger.</param>
    public SemanticSearchSettingsViewModel(
        ISemanticCapabilityService capability,
        IEmbeddingModelInstaller installer,
        ISemanticIndexControl control,
        ISemanticSearchReadModel progress,
        IEmbeddingErasureService erasure,
        UserPreferencesController preferences,
        ILocalizationService localization,
        IUiDispatcher dispatcher,
        ILogger logger)
    {
        _capability = capability ?? throw new ArgumentNullException(nameof(capability));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _erasure = erasure ?? throw new ArgumentNullException(nameof(erasure));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localization.CultureChanged += OnCultureChanged;
        _capability.Changed += OnCapabilityChanged;
        _control.Changed += OnControlChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The last provider change or check started from a binding (tests await it).</summary>
    public Task PendingWork => _pendingWork;

    // ── Provider ──────────────────────────────────────────────────────────────

    /// <summary>The built-in model is chosen.</summary>
    public bool IsBuiltIn
    {
        get => _capability.Choice == SemanticProviderChoice.BuiltIn;
        set => ChooseFromBinding(value, SemanticProviderChoice.BuiltIn);
    }

    /// <summary>Ollama is chosen.</summary>
    public bool IsOllama
    {
        get => _capability.Choice == SemanticProviderChoice.Ollama;
        set => ChooseFromBinding(value, SemanticProviderChoice.Ollama);
    }

    /// <summary>Smart search is switched off.</summary>
    public bool IsOff
    {
        get => _capability.Choice == SemanticProviderChoice.Off;
        set => ChooseFromBinding(value, SemanticProviderChoice.Off);
    }

    /// <summary>The provider can be chosen here (no environment override).</summary>
    public bool CanChooseProvider => !_capability.IsManagedByEnvironment;

    /// <summary>An environment override fixes the provider.</summary>
    public bool IsProviderManaged => _capability.IsManagedByEnvironment;

    /// <summary>Whether the built-in model card applies.</summary>
    public bool IsBuiltInSelected => IsBuiltIn;

    /// <summary>Whether the Ollama card applies.</summary>
    public bool IsOllamaSelected => IsOllama;

    // ── Built-in model ────────────────────────────────────────────────────────

    /// <summary>The built-in model's state.</summary>
    public SemanticModelState ModelState => _modelState;

    /// <summary>One honest line for the current model state.</summary>
    public string ModelStatusText => _modelStatusKey switch
    {
        "" => string.Empty,
        "Settings.Search.Model.NotInstalledFormat" => Format(_modelStatusKey, FormatMegabytes(_status?.DownloadSizeBytes ?? 0)),
        "Settings.Search.Model.DownloadingFormat" => Format(_modelStatusKey, Math.Floor(_downloadPercent)),
        _ => _localization[_modelStatusKey],
    };

    /// <summary>The state is a failure (error or offline).</summary>
    public bool HasError => _modelState is SemanticModelState.Error or SemanticModelState.Offline;

    /// <summary>Whether the consent text (size and destination) is shown before the Download button.</summary>
    public bool ShowDownloadConsent => IsBuiltIn && CanDownload;

    /// <summary>States the download size and the single destination host.</summary>
    public string DownloadConsentText => Format(
        "Settings.Search.Model.ConsentFormat",
        FormatMegabytes(_status?.DownloadSizeBytes ?? 0),
        string.IsNullOrEmpty(_status?.DownloadHost) ? "?" : _status!.DownloadHost);

    /// <summary>A download (or a retry) can start.</summary>
    public bool CanDownload => IsBuiltIn &&
        _modelState is SemanticModelState.NotInstalled or SemanticModelState.Error or SemanticModelState.Offline;

    /// <summary>The download button label: Download, or Retry after a failure.</summary>
    public string DownloadLabel => HasError
        ? _localization["Settings.Search.Model.Retry"]
        : _localization["Settings.Search.Model.Download"];

    /// <summary>A running download can be cancelled.</summary>
    public bool CanCancelDownload => _modelState is SemanticModelState.Downloading or SemanticModelState.Verifying;

    /// <summary>The installed (or damaged) model can be removed.</summary>
    public bool CanRemoveModel => IsBuiltIn && (_modelState == SemanticModelState.Ready ||
        (_modelState == SemanticModelState.Error && _status?.State == EmbeddingModelInstallState.Corrupt));

    /// <summary>Download progress, 0 to 100.</summary>
    public double DownloadPercent => _downloadPercent;

    /// <summary>Whether the download progress bar is shown.</summary>
    public bool IsDownloadProgressVisible => CanCancelDownload;

    /// <summary>Whether disk use is known (the model is installed).</summary>
    public bool HasDiskUse => IsBuiltIn && _modelState == SemanticModelState.Ready && _status is not null;

    /// <summary>Disk space the installed model uses.</summary>
    public string DiskUseText => Format("Settings.Search.Model.DiskUseFormat", FormatMegabytes(_status?.DiskUseBytes ?? 0));

    // ── Smart index ───────────────────────────────────────────────────────────

    /// <summary>Indexing can be started now.</summary>
    public bool CanBuildIndex => !IsOff && _capability.IsAvailable;

    /// <summary>Indexing can be paused.</summary>
    public bool CanPause => !IsOff && !_control.IsPaused;

    /// <summary>Indexing is paused and can be resumed.</summary>
    public bool CanResume => _control.IsPaused;

    /// <summary>Whether the index card shows progress (a provider is chosen).</summary>
    public bool IsIndexVisible => !IsOff;

    /// <summary>Books whose every chunk has a vector from the current model.</summary>
    public int BooksReady => Math.Max(0, _indexProgress.BooksTotal - _indexProgress.BooksWaiting);

    /// <summary>"n of m books ready".</summary>
    public string IndexProgressText => Format("Settings.Search.Index.ProgressFormat", BooksReady, _indexProgress.BooksTotal);

    /// <summary>Embedded chunks as a percentage of eligible chunks.</summary>
    public double IndexPercent => _indexProgress.ChunksTotal == 0
        ? 0
        : 100d * _indexProgress.ChunksEmbedded / _indexProgress.ChunksTotal;

    /// <summary>Time left, paused, done or unavailable.</summary>
    public string IndexEtaText
    {
        get
        {
            if (!_capability.IsAvailable)
            {
                return _localization["Settings.Search.Index.Unavailable"];
            }

            if (_control.IsPaused)
            {
                return _localization["Settings.Search.Index.Paused"];
            }

            if (_indexProgress.ChunksTotal > 0 && _indexProgress.ChunksEmbedded >= _indexProgress.ChunksTotal)
            {
                return _localization["Settings.Search.Index.Done"];
            }

            return _indexProgress.EstimatedRemaining switch
            {
                null => _localization["Settings.Search.Index.EtaUnknown"],
                { TotalMinutes: < 1 } => _localization["Settings.Search.Index.EtaUnderMinute"],
                { TotalMinutes: < 1.5 } => _localization["Settings.Search.Index.EtaOneMinute"],
                var left => Format("Settings.Search.Index.EtaMinutesFormat", (int)Math.Round(left.Value.TotalMinutes)),
            };
        }
    }

    // ── Erase ─────────────────────────────────────────────────────────────────

    /// <summary>Erasing is possible.</summary>
    public bool CanErase => !_isEraseConfirmationVisible;

    /// <summary>The erase confirmation is shown.</summary>
    public bool IsEraseConfirmationVisible => _isEraseConfirmationVisible;

    /// <summary>The outcome of the last erase.</summary>
    public string EraseStatusText => _eraseStatusText;

    /// <summary>Whether an erase outcome is shown.</summary>
    public bool HasEraseStatus => !string.IsNullOrEmpty(_eraseStatusText);

    // ── Ollama ────────────────────────────────────────────────────────────────

    /// <summary>Whether Ollama answered the last probe.</summary>
    public string OllamaStatusText => _capability.IsAvailable
        ? _localization["Settings.Search.Ollama.Detected"]
        : _localization["Settings.Search.Ollama.NotDetected"];

    /// <summary>Whether the setup guidance applies (Ollama not detected).</summary>
    public bool ShowOllamaGuidance => IsOllama && !_capability.IsAvailable;

    // ── Labels ────────────────────────────────────────────────────────────────

    /// <summary>Introductory text.</summary>
    public string BodyText => _localization["Settings.Search.Body"];

    /// <summary>Provider group label.</summary>
    public string ProviderLabel => _localization["Settings.Search.Provider"];

    /// <summary>Provider group help.</summary>
    public string ProviderHelp => _localization["Settings.Search.ProviderHelp"];

    /// <summary>Built-in option.</summary>
    public string BuiltInText => _localization["Settings.Search.Provider.BuiltIn"];

    /// <summary>Ollama option.</summary>
    public string OllamaText => _localization["Settings.Search.Provider.Ollama"];

    /// <summary>Off option.</summary>
    public string OffText => _localization["Settings.Search.Provider.Off"];

    /// <summary>Why the provider cannot be changed here.</summary>
    public string ProviderManagedText => _localization["Settings.Search.Provider.Managed"];

    /// <summary>The keyword-only line shown when smart search is off.</summary>
    public string KeywordOnlyText => _localization["Settings.Search.KeywordOnly"];

    /// <summary>Model card title.</summary>
    public string ModelLabel => _localization["Settings.Search.Model"];

    /// <summary>Cancel download label.</summary>
    public string CancelDownloadLabel => _localization["Settings.Search.Model.Cancel"];

    /// <summary>Remove model label.</summary>
    public string RemoveModelLabel => _localization["Settings.Search.Model.Remove"];

    /// <summary>Accessible name of the download progress bar.</summary>
    public string DownloadProgressName => _localization["Settings.Search.Model.ProgressName"];

    /// <summary>Index card title.</summary>
    public string IndexLabel => _localization["Settings.Search.Index"];

    /// <summary>Index card help.</summary>
    public string IndexHelp => _localization["Settings.Search.IndexHelp"];

    /// <summary>Build now label.</summary>
    public string BuildIndexLabel => _localization["Settings.Search.Index.Build"];

    /// <summary>Pause label.</summary>
    public string PauseLabel => _localization["Settings.Search.Index.Pause"];

    /// <summary>Resume label.</summary>
    public string ResumeLabel => _localization["Settings.Search.Index.Resume"];

    /// <summary>Accessible name of the index progress bar.</summary>
    public string IndexProgressName => _localization["Settings.Search.Index.ProgressName"];

    /// <summary>Erase button label.</summary>
    public string EraseLabel => _localization["Settings.Search.Erase"];

    /// <summary>Erase help.</summary>
    public string EraseHelp => _localization["Settings.Search.EraseHelp"];

    /// <summary>Erase confirmation question.</summary>
    public string EraseConfirmText => _localization["Settings.Search.Erase.Confirm"];

    /// <summary>Erase confirmation button.</summary>
    public string EraseConfirmLabel => _localization["Settings.Search.Erase.ConfirmButton"];

    /// <summary>Erase cancel button.</summary>
    public string EraseCancelLabel => _localization["Settings.Search.Erase.CancelButton"];

    /// <summary>Ollama card title.</summary>
    public string OllamaLabel => _localization["Settings.Search.Ollama"];

    /// <summary>Ollama setup guidance (no link is opened automatically).</summary>
    public string OllamaGuidanceText => _localization["Settings.Search.Ollama.Guidance"];

    /// <summary>Check again label.</summary>
    public string OllamaCheckLabel => _localization["Settings.Search.Ollama.Check"];

    // ── Operations ────────────────────────────────────────────────────────────

    /// <summary>Reads the model state and index progress and subscribes to index events. Never uses the network.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _subscription ??= _progress.Events.Subscribe(this);
        await RefreshModelStatusAsync(cancellationToken).ConfigureAwait(false);
        await RefreshProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists and applies a provider choice, then re-checks availability.</summary>
    /// <param name="choice">The new provider.</param>
    /// <param name="cancellationToken">Cancels the save and the check.</param>
    /// <returns>A task that completes when the choice is applied.</returns>
    public async Task SelectProviderAsync(SemanticProviderChoice choice, CancellationToken cancellationToken = default)
    {
        if (!CanChooseProvider)
        {
            await OnUiAsync(RaiseAll).ConfigureAwait(false);
            return;
        }

        await _preferences.UpdateAsync(current => current with { SemanticProvider = choice }, cancellationToken)
            .ConfigureAwait(true);
        if (choice != SemanticProviderChoice.Off)
        {
            await _capability.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        await OnUiAsync(RaiseAll).ConfigureAwait(false);
        await RefreshProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Downloads, verifies and installs the built-in model. Only this method touches the network.</summary>
    /// <param name="cancellationToken">Cancels the download (as <see cref="CancelDownload"/> does).</param>
    /// <returns>A task that completes when the install ends.</returns>
    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanDownload)
        {
            return;
        }

        using var download = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _download = download;
        await OnUiAsync(() =>
        {
            _downloadPercent = 0;
            SetModelState(SemanticModelState.Downloading, "Settings.Search.Model.DownloadingFormat");
        }).ConfigureAwait(false);

        var progress = new InlineProgress(OnDownloadProgress);
        try
        {
            EmbeddingModelInstallResult result = await _installer.InstallAsync(progress, download.Token)
                .ConfigureAwait(false);
            if (result.Succeeded)
            {
                await RefreshModelStatusAsync(CancellationToken.None).ConfigureAwait(false);
                await _capability.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                await RefreshProgressAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                (SemanticModelState state, string key) = result.Outcome switch
                {
                    EmbeddingModelInstallOutcome.Offline => (SemanticModelState.Offline, "Settings.Search.Model.Offline"),
                    EmbeddingModelInstallOutcome.ChecksumMismatch => (SemanticModelState.Error, "Settings.Search.Model.Error.Checksum"),
                    EmbeddingModelInstallOutcome.InsufficientSpace => (SemanticModelState.Error, "Settings.Search.Model.Error.Space"),
                    EmbeddingModelInstallOutcome.HostNotAllowed => (SemanticModelState.Error, "Settings.Search.Model.Error.Host"),
                    EmbeddingModelInstallOutcome.ServerError => (SemanticModelState.Error, "Settings.Search.Model.Error.Server"),
                    _ => (SemanticModelState.Error, "Settings.Search.Model.Error.Unexpected"),
                };
                await OnUiAsync(() => SetModelState(state, key)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await OnUiAsync(() => SetModelState(SemanticModelState.NotInstalled, "Settings.Search.Model.Cancelled"))
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(SemanticSearchSettingsViewModel), "settings.search.download");
            await OnUiAsync(() => SetModelState(SemanticModelState.Error, "Settings.Search.Model.Error.Unexpected"))
                .ConfigureAwait(false);
        }
        finally
        {
            _download = null;
        }
    }

    /// <summary>Stops a running download; downloaded parts are kept for a later resume.</summary>
    public void CancelDownload() => _download?.Cancel();

    /// <summary>Deletes the installed model and re-checks availability.</summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>A task that completes when the files are gone.</returns>
    public async Task RemoveModelAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRemoveModel)
        {
            return;
        }

        try
        {
            await _installer.RemoveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(SemanticSearchSettingsViewModel), "settings.search.remove");
        }

        await RefreshModelStatusAsync(cancellationToken).ConfigureAwait(false);
        await _capability.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resumes indexing and wakes the worker now.</summary>
    /// <param name="cancellationToken">Cancels the capability re-check.</param>
    /// <returns>A task that completes once the worker has been woken.</returns>
    public async Task BuildNowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanBuildIndex)
        {
            return;
        }

        await _control.BuildNowAsync(cancellationToken).ConfigureAwait(false);
        await RefreshProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pauses background indexing until resumed or restarted.</summary>
    public void Pause()
    {
        if (CanPause)
        {
            _control.Pause();
        }
    }

    /// <summary>Resumes background indexing.</summary>
    public void Resume()
    {
        if (CanResume)
        {
            _control.ResumeIndexing();
        }
    }

    /// <summary>Re-probes the chosen provider (the Ollama "Check again" action). Local only.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>A task that completes when the probe ends.</returns>
    public async Task CheckOllamaAsync(CancellationToken cancellationToken = default)
    {
        await _capability.RefreshAsync(cancellationToken).ConfigureAwait(false);
        await OnUiAsync(RaiseAll).ConfigureAwait(false);
    }

    /// <summary>Shows the erase confirmation; nothing is erased yet.</summary>
    public void RequestErase()
    {
        _isEraseConfirmationVisible = true;
        _eraseStatusText = string.Empty;
        RaiseErase();
    }

    /// <summary>Hides the erase confirmation without erasing.</summary>
    public void CancelErase()
    {
        _isEraseConfirmationVisible = false;
        RaiseErase();
    }

    /// <summary>Erases every embedding vector, only after <see cref="RequestErase"/>.</summary>
    /// <param name="cancellationToken">Cancels the erase.</param>
    /// <returns>A task that completes when the erase ends.</returns>
    public async Task ConfirmEraseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isEraseConfirmationVisible)
        {
            return;
        }

        string text;
        try
        {
            EmbeddingErasureResult result = await _erasure.EraseAllAsync(cancellationToken).ConfigureAwait(false);
            text = Format("Settings.Search.Erase.DoneFormat", result.BooksReset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(SemanticSearchSettingsViewModel), "settings.search.erase");
            text = _localization["Settings.Search.Erase.Failed"];
        }

        await OnUiAsync(() =>
        {
            _isEraseConfirmationVisible = false;
            _eraseStatusText = text;
            RaiseErase();
        }).ConfigureAwait(false);
        await RefreshProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-reads index progress.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the progress is shown.</returns>
    public async Task RefreshProgressAsync(CancellationToken cancellationToken = default)
    {
        SemanticIndexProgress progress = await _progress.GetProgressAsync(cancellationToken).ConfigureAwait(false);
        await OnUiAsync(() =>
        {
            _indexProgress = progress;
            RaiseIndex();
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void OnNext(SemanticIndexEvent value)
    {
        // Embeddings arrive in bursts on a background thread; refresh at most every two seconds.
        if (_disposed || Interlocked.Exchange(ref _progressRefreshQueued, 1) == 1)
        {
            return;
        }

        _ = RefreshProgressLaterAsync();
    }

    /// <inheritdoc />
    public void OnError(Exception error) =>
        AppLog.ViewModelStepIgnored(_logger, error, nameof(SemanticSearchSettingsViewModel), "settings.search.events");

    /// <inheritdoc />
    public void OnCompleted()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.CultureChanged -= OnCultureChanged;
        _capability.Changed -= OnCapabilityChanged;
        _control.Changed -= OnControlChanged;
        _subscription?.Dispose();
        _download?.Cancel();
    }

    private async Task RefreshProgressLaterAsync()
    {
        try
        {
            await Task.Delay(ProgressRefreshInterval).ConfigureAwait(false);
            Volatile.Write(ref _progressRefreshQueued, 0);
            if (!_disposed)
            {
                await RefreshProgressAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(SemanticSearchSettingsViewModel), "settings.search.progress");
        }
    }

    private async Task RefreshModelStatusAsync(CancellationToken cancellationToken)
    {
        EmbeddingModelInstallStatus status = await _installer.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        await OnUiAsync(() =>
        {
            _status = status;
            switch (status.State)
            {
                case EmbeddingModelInstallState.Installed:
                    SetModelState(SemanticModelState.Ready, "Settings.Search.Model.Ready");
                    break;
                case EmbeddingModelInstallState.Corrupt:
                    SetModelState(SemanticModelState.Error, "Settings.Search.Model.Error.Corrupt");
                    break;
                default:
                    SetModelState(SemanticModelState.NotInstalled, "Settings.Search.Model.NotInstalledFormat");
                    break;
            }
        }).ConfigureAwait(false);
    }

    private void OnDownloadProgress(EmbeddingModelInstallProgress value)
    {
        double percent = value.TotalBytes <= 0 ? 0 : Math.Clamp(100d * value.BytesReceived / value.TotalBytes, 0, 100);
        _dispatcher.Post(() =>
        {
            if (_modelState is not (SemanticModelState.Downloading or SemanticModelState.Verifying))
            {
                return;
            }

            _downloadPercent = percent;
            OnPropertyChanged(nameof(DownloadPercent));
            if (value.TotalBytes > 0 && value.BytesReceived >= value.TotalBytes)
            {
                SetModelState(SemanticModelState.Verifying, "Settings.Search.Model.Verifying");
            }
            else
            {
                OnPropertyChanged(nameof(ModelStatusText));
            }
        });
    }

    private void ChooseFromBinding(bool value, SemanticProviderChoice choice)
    {
        if (!value || _capability.Choice == choice)
        {
            return;
        }

        _pendingWork = UiActions.RunAsync(() => SelectProviderAsync(choice), "settings.search.provider");
    }

    private void SetModelState(SemanticModelState state, string key)
    {
        _modelState = state;
        _modelStatusKey = key;
        RaiseModel();
    }

    private void OnCapabilityChanged(object? sender, EventArgs e) => _dispatcher.Post(RaiseAll);

    private void OnControlChanged(object? sender, EventArgs e) => _dispatcher.Post(RaiseIndex);

    private void OnCultureChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!string.IsNullOrEmpty(_eraseStatusText))
        {
            _eraseStatusText = string.Empty;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    });

    private Task OnUiAsync(Action action) => _dispatcher.InvokeAsync(action);

    private string Format(string key, params object[] args) =>
        string.Format(_localization.CurrentCulture, _localization[key], args);

    private string FormatMegabytes(long bytes) =>
        Format("Settings.Search.SizeMbFormat", Math.Round(bytes / BytesPerMegabyte, 1));

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsBuiltIn));
        OnPropertyChanged(nameof(IsOllama));
        OnPropertyChanged(nameof(IsOff));
        OnPropertyChanged(nameof(IsBuiltInSelected));
        OnPropertyChanged(nameof(IsOllamaSelected));
        OnPropertyChanged(nameof(CanChooseProvider));
        OnPropertyChanged(nameof(IsProviderManaged));
        OnPropertyChanged(nameof(OllamaStatusText));
        OnPropertyChanged(nameof(ShowOllamaGuidance));
        RaiseModel();
        RaiseIndex();
    }

    private void RaiseModel()
    {
        OnPropertyChanged(nameof(ModelState));
        OnPropertyChanged(nameof(ModelStatusText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowDownloadConsent));
        OnPropertyChanged(nameof(DownloadConsentText));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(DownloadLabel));
        OnPropertyChanged(nameof(CanCancelDownload));
        OnPropertyChanged(nameof(CanRemoveModel));
        OnPropertyChanged(nameof(DownloadPercent));
        OnPropertyChanged(nameof(IsDownloadProgressVisible));
        OnPropertyChanged(nameof(HasDiskUse));
        OnPropertyChanged(nameof(DiskUseText));
    }

    private void RaiseIndex()
    {
        OnPropertyChanged(nameof(IsIndexVisible));
        OnPropertyChanged(nameof(CanBuildIndex));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(BooksReady));
        OnPropertyChanged(nameof(IndexProgressText));
        OnPropertyChanged(nameof(IndexPercent));
        OnPropertyChanged(nameof(IndexEtaText));
    }

    private void RaiseErase()
    {
        OnPropertyChanged(nameof(CanErase));
        OnPropertyChanged(nameof(IsEraseConfirmationVisible));
        OnPropertyChanged(nameof(EraseStatusText));
        OnPropertyChanged(nameof(HasEraseStatus));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Reports progress synchronously so the dispatcher, not a captured context, decides the thread.
    /// </summary>
    private sealed class InlineProgress(Action<EmbeddingModelInstallProgress> report) : IProgress<EmbeddingModelInstallProgress>
    {
        public void Report(EmbeddingModelInstallProgress value) => report(value);
    }
}
