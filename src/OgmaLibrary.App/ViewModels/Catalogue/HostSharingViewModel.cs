using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.LanHost;
using QRCoder;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>View model for the Phase 16 Host sharing control strip.</summary>
public sealed class HostSharingViewModel : INotifyPropertyChanged
{
    private readonly ILibraryHostService _hostService;
    private readonly IHostModeSettingsRepository _settingsRepository;
    private readonly IClassroomJoinParser? _joinParser;
    private readonly IClassroomConnectionService? _connectionService;
    private readonly ISyncService? _syncService;
    private readonly IClassroomModeService? _classroomModeService;
    private readonly IMdnsResolver? _mdnsResolver;
    private readonly IProfileService? _profileService;
    private readonly string _title = "Host";
    private readonly string _clientTitle = "Connect to Host";
    private readonly string _discoveredHostsLabel = "LAN Hosts";
    private readonly string _refreshHostsText = "Scan";
    private readonly string _syncNowText = "Sync now";
    private readonly string _syncOptInText = "Enable private sync";
    private readonly string _syncOnReconnectText = "Sync on reconnect";
    private readonly string _syncConflictsLabel = "Annotation conflicts";
    private readonly string _keepLocalText = "Keep local";
    private readonly string _keepServerText = "Keep server";
    private readonly string _joinLinkLabel = "Join link";
    private readonly string _savedProfileLabel = "Saved profile";
    private readonly string _profileDisplayNameLabel = "Student name";
    private readonly string _guestProfileText = "Guest";
    private readonly string _acceptTrustText = "Trust this Host fingerprint";
    private readonly string _connectToHostText = "Connect";
    private readonly string _sharePanelTitle = "Share Library Host";
    private readonly string _shareButtonText = "Share";
    private readonly string _copyJoinLinkText = "Copy join link";
    private readonly string _copyFingerprintText = "Copy fingerprint";
    private readonly string _closeSharePanelText = "Close";
    private readonly string _startConfirmationText =
        "Starting Host mode opens this library to authenticated devices on your local network.";
    private readonly string _fileStreamConfirmationText =
        "File Stream sends raw PDF files to clients. Page Render keeps PDF bytes on this computer.";
    private readonly string _confirmStartText = "Start Host";
    private readonly string _confirmFileStreamText = "Use File Stream";
    private readonly string _cancelConfirmationText = "Cancel";
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
    private bool _isStartConfirmationOpen;
    private bool _isFileStreamConfirmationOpen;
    private bool _acceptFirstUseTrust;
    private bool _useGuestProfile;
    private bool _isSyncEnabled;
    private bool _isSyncOptInEnabled;
    private bool _syncOnReconnect;
    private IReadOnlyList<DiscoveredClassroomHost> _discoveredHosts = [];
    private DiscoveredClassroomHost? _selectedDiscoveredHost;
    private IReadOnlyList<ClassroomProfile> _classroomProfiles = [];
    private ClassroomProfile? _selectedClassroomProfile;
    private IReadOnlyList<StudentAnnotationConflict> _pendingAnnotationConflicts = [];
    private StudentAnnotationConflict? _selectedAnnotationConflict;
    private string _joinLink = string.Empty;
    private string _profileDisplayName = string.Empty;
    private string _clientConnectionStatusText = "Not connected";
    private string _syncStatusText = "Sync unavailable";
    private string? _shareConfirmationText;

    public HostSharingViewModel(
        ILibraryHostService hostService,
        IHostModeSettingsRepository settingsRepository,
        IClassroomJoinParser? joinParser = null,
        IClassroomConnectionService? connectionService = null,
        ISyncService? syncService = null,
        IClassroomModeService? classroomModeService = null,
        IMdnsResolver? mdnsResolver = null,
        IProfileService? profileService = null)
    {
        _hostService = hostService ?? throw new ArgumentNullException(nameof(hostService));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _joinParser = joinParser;
        _connectionService = connectionService;
        _syncService = syncService;
        _classroomModeService = classroomModeService;
        _mdnsResolver = mdnsResolver;
        _profileService = profileService;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the Client-mode Host connection completes successfully.</summary>
    public event EventHandler<ClassroomConnectionResult>? HostConnectionSucceeded;

    public string Title => _title;

    public string ClientTitle => _clientTitle;

    public string DiscoveredHostsLabel => _discoveredHostsLabel;

    public string RefreshHostsText => _refreshHostsText;

    public string SyncNowText => _syncNowText;

    public string SyncOptInText => _syncOptInText;

    public string SyncOnReconnectText => _syncOnReconnectText;

    public string SyncConflictsLabel => _syncConflictsLabel;

    public string KeepLocalText => _keepLocalText;

    public string KeepServerText => _keepServerText;

    public string JoinLinkLabel => _joinLinkLabel;

    public string SavedProfileLabel => _savedProfileLabel;

    public string ProfileDisplayNameLabel => _profileDisplayNameLabel;

    public string GuestProfileText => _guestProfileText;

    public string AcceptTrustText => _acceptTrustText;

    public string ConnectToHostText => _connectToHostText;

    public string ClientConnectionStatusText
    {
        get => _clientConnectionStatusText;
        private set
        {
            if (_clientConnectionStatusText != value)
            {
                _clientConnectionStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string SyncStatusText
    {
        get => _syncStatusText;
        private set
        {
            if (_syncStatusText != value)
            {
                _syncStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string JoinLink
    {
        get => _joinLink;
        set
        {
            string next = value ?? string.Empty;
            if (_joinLink != next)
            {
                _joinLink = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConnectToHost));
            }
        }
    }

    public IReadOnlyList<DiscoveredClassroomHost> DiscoveredHosts
    {
        get => _discoveredHosts;
        private set
        {
            if (!ReferenceEquals(_discoveredHosts, value))
            {
                _discoveredHosts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDiscoveredHosts));
            }
        }
    }

    public DiscoveredClassroomHost? SelectedDiscoveredHost
    {
        get => _selectedDiscoveredHost;
        set
        {
            if (_selectedDiscoveredHost != value)
            {
                _selectedDiscoveredHost = value;
                OnPropertyChanged();
                if (value is not null)
                {
                    JoinLink = BuildJoinUri(
                        value.DisplayName,
                        value.Address,
                        value.Port,
                        value.CertificateFingerprint,
                        ResolveEnrollmentCode(value));
                    ClientConnectionStatusText = $"Selected {value.DisplayName}";
                }
            }
        }
    }

    public IReadOnlyList<ClassroomProfile> ClassroomProfiles
    {
        get => _classroomProfiles;
        private set
        {
            if (!ReferenceEquals(_classroomProfiles, value))
            {
                _classroomProfiles = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasClassroomProfiles));
            }
        }
    }

    public ClassroomProfile? SelectedClassroomProfile
    {
        get => _selectedClassroomProfile;
        set
        {
            if (_selectedClassroomProfile != value)
            {
                _selectedClassroomProfile = value;
                OnPropertyChanged();
                if (value is not null)
                {
                    UseGuestProfile = false;
                    ProfileDisplayName = value.DisplayName;
                }

                OnPropertyChanged(nameof(CanConnectToHost));
            }
        }
    }

    public IReadOnlyList<StudentAnnotationConflict> PendingAnnotationConflicts
    {
        get => _pendingAnnotationConflicts;
        private set
        {
            if (!ReferenceEquals(_pendingAnnotationConflicts, value))
            {
                _pendingAnnotationConflicts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPendingAnnotationConflicts));
                OnPropertyChanged(nameof(PendingConflictCountText));
            }
        }
    }

    public StudentAnnotationConflict? SelectedAnnotationConflict
    {
        get => _selectedAnnotationConflict;
        set
        {
            if (_selectedAnnotationConflict != value)
            {
                _selectedAnnotationConflict = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedConflictText));
                OnPropertyChanged(nameof(CanResolveAnnotationConflict));
            }
        }
    }

    public string PendingConflictCountText => PendingAnnotationConflicts.Count == 1
        ? "1 annotation conflict needs a choice"
        : $"{PendingAnnotationConflicts.Count} annotation conflicts need a choice";

    public string SelectedConflictText => SelectedAnnotationConflict is null
        ? string.Empty
        : FormatAnnotationConflict(SelectedAnnotationConflict);

    public string ProfileDisplayName
    {
        get => _profileDisplayName;
        set
        {
            string next = value ?? string.Empty;
            if (_profileDisplayName != next)
            {
                _profileDisplayName = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConnectToHost));
            }
        }
    }

    public bool AcceptFirstUseTrust
    {
        get => _acceptFirstUseTrust;
        set
        {
            if (_acceptFirstUseTrust != value)
            {
                _acceptFirstUseTrust = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseGuestProfile
    {
        get => _useGuestProfile;
        set
        {
            if (_useGuestProfile != value)
            {
                _useGuestProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConnectToHost));
            }
        }
    }

    public bool IsSyncOptInEnabled
    {
        get => _isSyncOptInEnabled;
        set
        {
            if (_isSyncOptInEnabled != value)
            {
                _isSyncOptInEnabled = value;
                if (!_isSyncOptInEnabled)
                {
                    SyncOnReconnect = false;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSyncNow));
                OnPropertyChanged(nameof(CanSyncOnReconnect));
            }
        }
    }

    public bool SyncOnReconnect
    {
        get => _syncOnReconnect;
        set
        {
            bool next = value && IsSyncOptInEnabled;
            if (_syncOnReconnect != next)
            {
                _syncOnReconnect = next;
                OnPropertyChanged();
            }
        }
    }

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

    public string StartConfirmationText => _startConfirmationText;

    public string FileStreamConfirmationText => _fileStreamConfirmationText;

    public string ConfirmStartText => _confirmStartText;

    public string ConfirmFileStreamText => _confirmFileStreamText;

    public string CancelConfirmationText => _cancelConfirmationText;

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
                OnPropertyChanged(nameof(CanChangeContentMode));
                OnPropertyChanged(nameof(CanConnectToHost));
                OnPropertyChanged(nameof(CanSyncNow));
                OnPropertyChanged(nameof(CanDiscoverHosts));
                OnPropertyChanged(nameof(CanResolveAnnotationConflict));
            }
        }
    }

    public bool CanStart => !IsBusy && !IsRunning && !IsStartConfirmationOpen;

    public bool CanStop => !IsBusy && IsRunning;

    public bool CanChangeContentMode => !IsBusy && !IsRunning && !IsFileStreamConfirmationOpen;

    public bool CanConnectToHost =>
        !IsBusy &&
        _joinParser is not null &&
        _connectionService is not null &&
        !string.IsNullOrWhiteSpace(JoinLink) &&
        (UseGuestProfile || !string.IsNullOrWhiteSpace(ProfileDisplayName));

    public bool CanDiscoverHosts => !IsBusy && _mdnsResolver is not null;

    public bool HasHostDiscovery => _mdnsResolver is not null;

    public bool HasDiscoveredHosts => DiscoveredHosts.Count > 0;

    public bool HasProfileManagement => _profileService is not null;

    public bool HasClassroomProfiles => ClassroomProfiles.Count > 0;

    public bool HasPendingAnnotationConflicts => PendingAnnotationConflicts.Count > 0;

    public bool CanSyncNow =>
        !IsBusy &&
        CanSyncWhenIdle;

    public bool CanResolveAnnotationConflict =>
        !IsBusy &&
        _syncService is not null &&
        SelectedAnnotationConflict is not null;

    private bool CanSyncWhenIdle =>
        _syncService is not null &&
        _isSyncEnabled &&
        (_classroomModeService is null || IsSyncOptInEnabled);

    public bool CanSyncOnReconnect => _classroomModeService is not null && IsSyncOptInEnabled;

    public bool HasPersistentSyncSettings => _classroomModeService is not null;

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

    public bool IsStartConfirmationOpen
    {
        get => _isStartConfirmationOpen;
        private set
        {
            if (_isStartConfirmationOpen != value)
            {
                _isStartConfirmationOpen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStart));
            }
        }
    }

    public bool IsFileStreamConfirmationOpen
    {
        get => _isFileStreamConfirmationOpen;
        private set
        {
            if (_isFileStreamConfirmationOpen != value)
            {
                _isFileStreamConfirmationOpen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangeContentMode));
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
        await RefreshProfilesAsync(cancellationToken).ConfigureAwait(false);
        await RefreshSyncStatusAsync(cancellationToken).ConfigureAwait(false);
        RaiseStatusChanged();
    }

    public async Task ConnectToHostAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConnectToHost || _joinParser is null || _connectionService is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (!_joinParser.TryParse(JoinLink, out ClassroomJoinRequest? joinRequest, out string? errorMessage) ||
                joinRequest is null)
            {
                ClientConnectionStatusText = errorMessage ?? "Join link is invalid.";
                return;
            }

            ClassroomConnectionResult result = await _connectionService
                .ConnectAsync(
                    new ClassroomConnectionRequest(
                        joinRequest,
                        AcceptFirstUseTrust,
                        ProfileId: UseGuestProfile ? null : SelectedClassroomProfile?.ProfileId,
                        ProfileDisplayName: UseGuestProfile ? null : ProfileDisplayName.Trim(),
                        UseGuestProfile: UseGuestProfile),
                    cancellationToken)
                .ConfigureAwait(true);

            if (result.IsConnected)
            {
                ClientConnectionStatusText = joinRequest.DisplayName is { Length: > 0 } displayName
                    ? $"Connected to {displayName}"
                    : "Connected to classroom Host";
                await RefreshSyncStatusAsync(cancellationToken).ConfigureAwait(true);
                if (SyncOnReconnect && CanSyncWhenIdle)
                {
                    await RunSyncNowAsync(cancellationToken).ConfigureAwait(true);
                }

                HostConnectionSucceeded?.Invoke(this, result);
                return;
            }

            ClientConnectionStatusText = result.ErrorMessage ?? result.TrustState switch
            {
                HostTrustState.FirstUse => "Trust this Host fingerprint before connecting.",
                HostTrustState.Mismatch => "Host fingerprint does not match the trusted pin.",
                _ => "Could not connect to classroom Host.",
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            ClientConnectionStatusText = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanConnectToHost));
            OnPropertyChanged(nameof(CanSyncNow));
        }
    }

    public async Task DiscoverHostsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanDiscoverHosts || _mdnsResolver is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            ClientConnectionStatusText = "Scanning for classroom Hosts";
            IReadOnlyList<DiscoveredClassroomHost> hosts = await _mdnsResolver
                .DiscoverAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(true);
            DiscoveredHosts = hosts;
            SelectedDiscoveredHost = hosts.Count == 1 ? hosts[0] : null;
            ClientConnectionStatusText = hosts.Count switch
            {
                0 => "No classroom Hosts found",
                1 => $"Selected {hosts[0].DisplayName}",
                _ => $"{hosts.Count} classroom Hosts found",
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            ClientConnectionStatusText = $"Host scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanDiscoverHosts));
            OnPropertyChanged(nameof(CanConnectToHost));
        }
    }

    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSyncNow || _syncService is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await RunSyncNowAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSyncNow));
        }
    }

    public async Task KeepLocalAnnotationConflictAsync(CancellationToken cancellationToken = default) =>
        await ResolveSelectedAnnotationConflictAsync(
                ClassroomSyncConflictResolution.KeepLocal,
                cancellationToken)
            .ConfigureAwait(true);

    public async Task KeepServerAnnotationConflictAsync(CancellationToken cancellationToken = default) =>
        await ResolveSelectedAnnotationConflictAsync(
                ClassroomSyncConflictResolution.KeepServer,
                cancellationToken)
            .ConfigureAwait(true);

    public async Task SaveSyncSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_classroomModeService is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new ClassroomSyncSettings(IsSyncOptInEnabled, SyncOnReconnect);
            await _classroomModeService.SaveSyncSettingsAsync(settings, cancellationToken).ConfigureAwait(true);
            ApplySyncSettings(settings.IsEnabled ? settings : settings with { SyncOnReconnect = false });
            await RefreshSyncStatusAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSyncNow));
            OnPropertyChanged(nameof(CanSyncOnReconnect));
        }
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
            IsStartConfirmationOpen = false;
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
            IsStartConfirmationOpen = false;
            IsFileStreamConfirmationOpen = false;
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
            IsFileStreamConfirmationOpen = false;
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

    public void RequestStartConfirmation()
    {
        if (!CanStart)
        {
            return;
        }

        IsStartConfirmationOpen = true;
    }

    public void CancelStartConfirmation() => IsStartConfirmationOpen = false;

    public async Task ConfirmStartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStartConfirmationOpen)
        {
            return;
        }

        IsStartConfirmationOpen = false;
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RequestContentModeChangeAsync(CancellationToken cancellationToken = default)
    {
        if (!CanChangeContentMode)
        {
            return;
        }

        _settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        if (_settings.ContentMode == HostContentDeliveryMode.PageRender)
        {
            IsFileStreamConfirmationOpen = true;
            return;
        }

        await ToggleContentModeAsync(cancellationToken).ConfigureAwait(false);
    }

    public void CancelFileStreamConfirmation() => IsFileStreamConfirmationOpen = false;

    public async Task ConfirmFileStreamAsync(CancellationToken cancellationToken = default)
    {
        if (!IsFileStreamConfirmationOpen)
        {
            return;
        }

        IsFileStreamConfirmationOpen = false;
        await ToggleContentModeAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task RunSyncNowAsync(CancellationToken cancellationToken)
    {
        if (_syncService is null)
        {
            return;
        }

        try
        {
            SyncStatusText = "Syncing";
            ClassroomSyncStatus status = await _syncService.SyncNowAsync(cancellationToken).ConfigureAwait(true);
            ApplySyncStatus(status);
            await RefreshAnnotationConflictsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            SyncStatusText = $"Sync failed: {ex.Message}";
        }
    }

    private async Task RefreshSyncStatusAsync(CancellationToken cancellationToken)
    {
        if (_classroomModeService is not null)
        {
            ClassroomSyncSettings settings = await _classroomModeService
                .GetSyncSettingsAsync(cancellationToken)
                .ConfigureAwait(true);
            ApplySyncSettings(settings);
        }

        if (_syncService is null)
        {
            SyncStatusText = "Sync unavailable";
            PendingAnnotationConflicts = [];
            SelectedAnnotationConflict = null;
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        if (_classroomModeService is not null && !IsSyncOptInEnabled)
        {
            _isSyncEnabled = false;
            SyncStatusText = "Private sync is off";
            PendingAnnotationConflicts = [];
            SelectedAnnotationConflict = null;
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        ClassroomSyncStatus status = await _syncService.GetStatusAsync(cancellationToken).ConfigureAwait(true);
        ApplySyncStatus(status);
        await RefreshAnnotationConflictsAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanSyncNow));
    }

    private async Task ResolveSelectedAnnotationConflictAsync(
        ClassroomSyncConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        if (!CanResolveAnnotationConflict || _syncService is null || SelectedAnnotationConflict is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            ClassroomSyncStatus status = await _syncService
                .ResolveAnnotationConflictAsync(
                    SelectedAnnotationConflict.LocalAnnotation.Id,
                    resolution,
                    cancellationToken)
                .ConfigureAwait(true);
            ApplySyncStatus(status);
            await RefreshAnnotationConflictsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            SyncStatusText = $"Conflict choice failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanResolveAnnotationConflict));
        }
    }

    private async Task RefreshAnnotationConflictsAsync(CancellationToken cancellationToken)
    {
        if (_syncService is null)
        {
            PendingAnnotationConflicts = [];
            SelectedAnnotationConflict = null;
            return;
        }

        string? selectedId = SelectedAnnotationConflict?.LocalAnnotation.Id;
        IReadOnlyList<StudentAnnotationConflict> conflicts = await _syncService
            .ListAnnotationConflictsAsync(cancellationToken)
            .ConfigureAwait(true);
        PendingAnnotationConflicts = conflicts;
        SelectedAnnotationConflict = conflicts.FirstOrDefault(conflict =>
            string.Equals(
                conflict.LocalAnnotation.Id,
                selectedId,
                StringComparison.Ordinal)) ?? (conflicts.Count > 0 ? conflicts[0] : null);
    }

    private async Task RefreshProfilesAsync(CancellationToken cancellationToken)
    {
        if (_profileService is null)
        {
            return;
        }

        IReadOnlyList<ClassroomProfile> profiles = await _profileService.ListAsync(cancellationToken).ConfigureAwait(true);
        ClassroomProfiles = profiles;
        ClassroomProfile? active = await _profileService.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        if (active is { IsGuest: false })
        {
            SelectedClassroomProfile = profiles.FirstOrDefault(profile => profile.ProfileId == active.ProfileId);
        }
        else if (active is { IsGuest: true })
        {
            UseGuestProfile = true;
        }
    }

    private void ApplySyncSettings(ClassroomSyncSettings settings)
    {
        IsSyncOptInEnabled = settings.IsEnabled;
        SyncOnReconnect = settings.IsEnabled && settings.SyncOnReconnect;
        OnPropertyChanged(nameof(CanSyncOnReconnect));
    }

    private void ApplySyncStatus(ClassroomSyncStatus status)
    {
        if (!status.IsEnabled)
        {
            _isSyncEnabled = false;
            SyncStatusText = string.IsNullOrWhiteSpace(status.ErrorMessage)
                ? "Sync unavailable"
                : $"Sync unavailable: {status.ErrorMessage}";
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        _isSyncEnabled = true;
        if (status.IsRunning)
        {
            SyncStatusText = "Syncing";
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        string conflictText = status.ConflictCount == 1
            ? "1 conflict"
            : $"{status.ConflictCount} conflicts";
        SyncStatusText = status.LastSyncedUtc is null
            ? $"Ready to sync, {conflictText}"
            : string.Format(
                CultureInfo.InvariantCulture,
                "Last synced {0:yyyy-MM-dd HH:mm} UTC, {1}",
                status.LastSyncedUtc.Value.UtcDateTime,
                conflictText);
        OnPropertyChanged(nameof(CanSyncNow));
    }

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
        OnPropertyChanged(nameof(StartConfirmationText));
        OnPropertyChanged(nameof(FileStreamConfirmationText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanChangeContentMode));
        OnPropertyChanged(nameof(CanShare));
        OnPropertyChanged(nameof(CanDiscoverHosts));
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
        string? enrollmentCode)
    {
        string query =
            $"name={Uri.EscapeDataString(displayName)}" +
            $"&fp={Uri.EscapeDataString(fingerprint)}";
        if (!string.IsNullOrWhiteSpace(enrollmentCode))
        {
            query += $"&code={Uri.EscapeDataString(enrollmentCode)}";
        }

        query += "&auth=enrollment-code";
        var builder = new UriBuilder("ogma-lan", hostAddress, port)
        {
            Path = "join",
            Query = query,
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string? ResolveEnrollmentCode(DiscoveredClassroomHost host)
    {
        if (host.Txt.TryGetValue("code", out string? code) && !string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        return host.Txt.TryGetValue("enrollment-code", out string? enrollmentCode) &&
            !string.IsNullOrWhiteSpace(enrollmentCode)
            ? enrollmentCode
            : null;
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

    private static string FormatAnnotationConflict(StudentAnnotationConflict conflict) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}, page {1}: local \"{2}\" / server \"{3}\"",
            conflict.LocalAnnotation.BookId,
            conflict.LocalAnnotation.PageNumber,
            TruncateConflictText(conflict.LocalAnnotation.Body),
            TruncateConflictText(conflict.RemoteAnnotation.Body));

    private static string TruncateConflictText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        string trimmed = value.Trim();
        return trimmed.Length <= 80
            ? trimmed
            : trimmed[..77] + "...";
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
