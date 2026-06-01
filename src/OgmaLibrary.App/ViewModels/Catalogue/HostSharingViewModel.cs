using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using OgmaLibrary.Application.LanHost;
using QRCoder;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>View model for the Phase 16 Host sharing control strip.</summary>
public sealed class HostSharingViewModel : INotifyPropertyChanged
{
    private readonly ILibraryHostService _hostService;
    private readonly IHostModeSettingsRepository _settingsRepository;
    private readonly string _title = "Host";
    private readonly string _sharePanelTitle = "Share Library Host";
    private readonly string _shareButtonText = "Share";
    private readonly string _copyJoinLinkText = "Copy join link";
    private readonly string _copyFingerprintText = "Copy fingerprint";
    private readonly string _closeSharePanelText = "Close";
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
    private bool _isSharePanelOpen;
    private string? _shareConfirmationText;

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

    public string FullFingerprintText =>
        string.IsNullOrWhiteSpace(_status.CertificateFingerprint)
            ? "No fingerprint available"
            : FormatFingerprint(_status.CertificateFingerprint);

    public string ManualJoinUri => CanShare
        ? BuildJoinUri(
            _settings.DisplayName,
            _status.HostAddress!,
            _status.Port,
            _status.CertificateFingerprint!,
            _status.EnrollmentCode!)
        : string.Empty;

    public string QrCodeText => CanShare ? BuildQrCodeText(ManualJoinUri) : string.Empty;

    public string SharePanelTitle => _sharePanelTitle;

    public string SharePanelSubtitle => CanShare
        ? $"Scan or copy the join link for {_settings.DisplayName}."
        : "Start the Host before sharing.";

    public string EnrollmentCodeText => string.IsNullOrWhiteSpace(_status.EnrollmentCode)
        ? "No enrollment code"
        : $"Enrollment code: {_status.EnrollmentCode}";

    public string ShareButtonText => _shareButtonText;

    public string CopyJoinLinkText => _copyJoinLinkText;

    public string CopyFingerprintText => _copyFingerprintText;

    public string CloseSharePanelText => _closeSharePanelText;

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

    public bool CanShare =>
        IsRunning &&
        !string.IsNullOrWhiteSpace(_status.HostAddress) &&
        !string.IsNullOrWhiteSpace(_status.CertificateFingerprint) &&
        !string.IsNullOrWhiteSpace(_status.EnrollmentCode);

    public bool IsSharePanelOpen
    {
        get => _isSharePanelOpen;
        private set
        {
            if (_isSharePanelOpen != value)
            {
                _isSharePanelOpen = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ShareConfirmationText
    {
        get => _shareConfirmationText;
        private set
        {
            if (_shareConfirmationText != value)
            {
                _shareConfirmationText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasShareConfirmation));
            }
        }
    }

    public bool HasShareConfirmation => !string.IsNullOrWhiteSpace(ShareConfirmationText);

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
            if (!CanShare)
            {
                IsSharePanelOpen = false;
            }

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

    public void OpenSharePanel()
    {
        if (!CanShare)
        {
            return;
        }

        ShareConfirmationText = null;
        IsSharePanelOpen = true;
        RaiseShareChanged();
    }

    public void CloseSharePanel()
    {
        IsSharePanelOpen = false;
        ShareConfirmationText = null;
    }

    public void MarkJoinLinkCopied() => ShareConfirmationText = "Join link copied to clipboard";

    public void MarkFingerprintCopied() => ShareConfirmationText = "Fingerprint copied to clipboard";

    public void ReportClipboardUnavailable() => ShareConfirmationText = "Clipboard is unavailable";

    private void RaiseStatusChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ClientCountText));
        OnPropertyChanged(nameof(FingerprintText));
        OnPropertyChanged(nameof(FullFingerprintText));
        OnPropertyChanged(nameof(ManualJoinUri));
        OnPropertyChanged(nameof(QrCodeText));
        OnPropertyChanged(nameof(SharePanelSubtitle));
        OnPropertyChanged(nameof(EnrollmentCodeText));
        OnPropertyChanged(nameof(ContentModeText));
        OnPropertyChanged(nameof(ToggleContentModeText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanChangeContentMode));
        OnPropertyChanged(nameof(CanShare));
    }

    private void RaiseShareChanged()
    {
        OnPropertyChanged(nameof(ManualJoinUri));
        OnPropertyChanged(nameof(QrCodeText));
        OnPropertyChanged(nameof(SharePanelSubtitle));
        OnPropertyChanged(nameof(EnrollmentCodeText));
        OnPropertyChanged(nameof(FullFingerprintText));
    }

    private static string BuildJoinUri(
        string displayName,
        string hostAddress,
        int port,
        string fingerprint,
        string enrollmentCode)
    {
        var builder = new UriBuilder("ogma-lan", hostAddress, port)
        {
            Path = "join",
            Query =
                $"name={Uri.EscapeDataString(displayName)}" +
                $"&fp={Uri.EscapeDataString(fingerprint)}" +
                $"&code={Uri.EscapeDataString(enrollmentCode)}" +
                "&auth=enrollment-code",
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string FormatFingerprint(string fingerprint)
    {
        var builder = new StringBuilder(fingerprint.Length + (fingerprint.Length / 4));
        for (int i = 0; i < fingerprint.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToUpperInvariant(fingerprint[i]));
        }

        return builder.ToString();
    }

    private static string BuildQrCodeText(string payload)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var builder = new StringBuilder();
        const int quietZone = 2;
        int size = data.ModuleMatrix.Count;

        for (int y = -quietZone; y < size + quietZone; y++)
        {
            for (int x = -quietZone; x < size + quietZone; x++)
            {
                bool isDark = y >= 0 &&
                              y < size &&
                              x >= 0 &&
                              x < data.ModuleMatrix[y].Count &&
                              data.ModuleMatrix[y][x];
                builder.Append(isDark ? "\u2588\u2588" : "  ");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
