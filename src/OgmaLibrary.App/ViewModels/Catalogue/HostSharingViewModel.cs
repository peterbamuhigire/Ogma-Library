using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>View model for the Phase 16 Host sharing control strip.</summary>
public sealed class HostSharingViewModel : INotifyPropertyChanged
{
    private readonly ILibraryHostService _hostService;
    private readonly IHostModeSettingsRepository _settingsRepository;
    private readonly string _title = "Host";
    private HostModeSettings _settings = new(
        IsEnabled: false,
        Port: 7473,
        ContentMode: HostContentDeliveryMode.PageRender,
        DisplayName: "Ogma Library");
    private LibraryHostStatus _status = new(
        LibraryHostState.Stopped,
        Port: 7473,
        ConnectedClientCount: 0,
        CertificateFingerprint: null,
        ErrorMessage: null);
    private bool _isBusy;

    public HostSharingViewModel(
        ILibraryHostService hostService,
        IHostModeSettingsRepository settingsRepository)
    {
        _hostService = hostService ?? throw new ArgumentNullException(nameof(hostService));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => _title;

    public string StatusText => _status.State switch
    {
        LibraryHostState.Running => $"Running on :{_status.Port}",
        LibraryHostState.Starting => "Starting",
        LibraryHostState.Error => _status.ErrorMessage ?? "Error",
        _ => "Stopped",
    };

    public string ClientCountText => $"{_status.ConnectedClientCount} clients";

    public string FingerprintText =>
        string.IsNullOrWhiteSpace(_status.CertificateFingerprint)
            ? "No fingerprint"
            : _status.CertificateFingerprint[..Math.Min(12, _status.CertificateFingerprint.Length)];

    public string ContentModeText => _settings.ContentMode == HostContentDeliveryMode.PageRender
        ? "Page Render"
        : "File Stream";

    public string ToggleContentModeText => _settings.ContentMode == HostContentDeliveryMode.PageRender
        ? "Use File Stream"
        : "Use Page Render";

    public string PrimaryActionText => IsRunning ? "Stop" : "Start";

    public bool IsRunning => _status.State == LibraryHostState.Running;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
            }
        }
    }

    public bool CanStart => !IsBusy && !IsRunning;

    public bool CanStop => !IsBusy && IsRunning;

    public bool CanChangeContentMode => !IsBusy && !IsRunning;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        _status = await _hostService.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        RaiseStatusChanged();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStart)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
            _status = await _hostService.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            RaiseStatusChanged();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStop)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _status = await _hostService.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            RaiseStatusChanged();
        }
    }

    public async Task ToggleContentModeAsync(CancellationToken cancellationToken = default)
    {
        if (!CanChangeContentMode)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
            HostContentDeliveryMode nextMode = _settings.ContentMode == HostContentDeliveryMode.PageRender
                ? HostContentDeliveryMode.FileStream
                : HostContentDeliveryMode.PageRender;
            _settings = _settings with { ContentMode = nextMode };
            await _settingsRepository.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
            RaiseStatusChanged();
        }
    }

    private void RaiseStatusChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ClientCountText));
        OnPropertyChanged(nameof(FingerprintText));
        OnPropertyChanged(nameof(ContentModeText));
        OnPropertyChanged(nameof(ToggleContentModeText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanChangeContentMode));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
