using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using OgmaLibrary.Application;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using QRCoder;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>View model for the Phase 16 Host sharing control strip.</summary>
public sealed class HostSharingViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ILibraryHostService _hostService;
    private readonly IHostModeSettingsRepository _settingsRepository;
    private readonly IClassroomJoinParser? _joinParser;
    private readonly IClassroomConnectionService? _connectionService;
    private readonly ISyncService? _syncService;
    private readonly IClassroomModeService? _classroomModeService;
    private readonly IMdnsResolver? _mdnsResolver;
    private readonly IProfileService? _profileService;
    private readonly IProfileEnrollmentService? _profileEnrollmentService;
    private readonly ISchoolAiKeyProvider? _schoolAiKeyProvider;
    private readonly ISchoolAiPolicyService? _schoolAiPolicyService;
    private readonly IUsageDashboardService? _usageDashboardService;
    private readonly ISchoolAiHistoryManagementService? _schoolAiHistoryManagementService;
    private readonly IAuditRepository? _auditRepository;
    private readonly ILocalizationService? _localization;
    private readonly string _title = "Host";
    private readonly string _schoolAdminTitle = "School administration";
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
    private string _schoolAiProviderId = "openai";
    private string _schoolAiKeyStatusText = "AI key status unavailable";
    private string _schoolAdminStatusText = "School administration unavailable";
    private string _enrollmentDisplayName = string.Empty;
    private string _enrollmentRole = "student";
    private string _enrollmentBirthYearText = string.Empty;
    private string _lastEnrollmentTokenText = string.Empty;
    private string _aiHistoryPurgeConfirmationText = string.Empty;
    private string _schoolAuditFilterText = string.Empty;
    private List<SchoolAuditRow> _allSchoolAuditEvents = [];
    private EnrolledProfile? _selectedEnrolledProfile;
    private int _perStudentDailyTokenBudget = 10_000;
    private int _classDailyTokenBudget = 500_000;
    private int _perStudentQueriesPerMinute = 5;

    public HostSharingViewModel(
        ILibraryHostService hostService,
        IHostModeSettingsRepository settingsRepository,
        IClassroomJoinParser? joinParser = null,
        IClassroomConnectionService? connectionService = null,
        ISyncService? syncService = null,
        IClassroomModeService? classroomModeService = null,
        IMdnsResolver? mdnsResolver = null,
        IProfileService? profileService = null,
        IProfileEnrollmentService? profileEnrollmentService = null,
        ISchoolAiKeyProvider? schoolAiKeyProvider = null,
        ISchoolAiPolicyService? schoolAiPolicyService = null,
        IUsageDashboardService? usageDashboardService = null,
        ISchoolAiHistoryManagementService? schoolAiHistoryManagementService = null,
        IAuditRepository? auditRepository = null,
        ILocalizationService? localization = null)
    {
        _hostService = hostService ?? throw new ArgumentNullException(nameof(hostService));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _joinParser = joinParser;
        _connectionService = connectionService;
        _syncService = syncService;
        _classroomModeService = classroomModeService;
        _mdnsResolver = mdnsResolver;
        _profileService = profileService;
        _profileEnrollmentService = profileEnrollmentService;
        _schoolAiKeyProvider = schoolAiKeyProvider;
        _schoolAiPolicyService = schoolAiPolicyService;
        _usageDashboardService = usageDashboardService;
        _schoolAiHistoryManagementService = schoolAiHistoryManagementService;
        _auditRepository = auditRepository;
        _localization = localization;
        if (_localization is not null)
        {
            _clientConnectionStatusText = _localization["Sharing.Client.NotConnected"];
            _syncStatusText = _localization["Sharing.Sync.Unavailable"];
            _schoolAiKeyStatusText = _localization["Sharing.SchoolAi.StatusUnavailable"];
            _schoolAdminStatusText = _localization["Sharing.SchoolAdmin.Unavailable"];
            _localization.CultureChanged += OnCultureChanged;
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the Client-mode Host connection completes successfully.</summary>
    public event EventHandler<ClassroomConnectionResult>? HostConnectionSucceeded;

    public string Title => Localize("Sharing.Host.Title", _title);

    public string SchoolAdminTitle => Localize("Sharing.SchoolAdmin.Title", _schoolAdminTitle);

    public string ClientTitle => Localize("Sharing.Client.Title", _clientTitle);

    public string DiscoveredHostsLabel => Localize("Sharing.Client.DiscoveredHosts", _discoveredHostsLabel);

    public string RefreshHostsText => Localize("Sharing.Client.RefreshHosts", _refreshHostsText);

    public string SyncNowText => Localize("Sharing.Sync.Now", _syncNowText);

    public string SyncOptInText => Localize("Sharing.Sync.OptIn", _syncOptInText);

    public string SyncOnReconnectText => Localize("Sharing.Sync.OnReconnect", _syncOnReconnectText);

    public string SyncConflictsLabel => Localize("Sharing.Sync.Conflicts", _syncConflictsLabel);

    public string KeepLocalText => Localize("Sharing.Sync.KeepLocal", _keepLocalText);

    public string KeepServerText => Localize("Sharing.Sync.KeepServer", _keepServerText);

    public string JoinLinkLabel => Localize("Sharing.Client.JoinLink", _joinLinkLabel);

    public string SavedProfileLabel => Localize("Sharing.Client.SavedProfile", _savedProfileLabel);

    public string ProfileDisplayNameLabel => Localize("Sharing.Client.ProfileName", _profileDisplayNameLabel);

    public string GuestProfileText => Localize("Sharing.Client.Guest", _guestProfileText);

    public string AcceptTrustText => Localize("Sharing.Client.AcceptTrust", _acceptTrustText);

    public string ConnectToHostText => Localize("Sharing.Client.Connect", _connectToHostText);

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
                    ClientConnectionStatusText = LocalizeFormat("Sharing.Client.SelectedHost", "Selected {0}", value.DisplayName);
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

    public ObservableCollection<EnrolledProfile> EnrolledProfiles { get; } = [];

    public ObservableCollection<UsageDashboardEntry> SchoolAiUsage { get; } = [];

    public ObservableCollection<SchoolAuditRow> SchoolAuditEvents { get; } = [];

    public EnrolledProfile? SelectedEnrolledProfile
    {
        get => _selectedEnrolledProfile;
        set
        {
            if (_selectedEnrolledProfile != value)
            {
                _selectedEnrolledProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRevokeSelectedProfile));
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
        ? Localize("Sharing.Sync.OneAnnotationConflict", "1 annotation conflict needs a choice")
        : LocalizeFormat("Sharing.Sync.ManyAnnotationConflicts", "{0} annotation conflicts need a choice", PendingAnnotationConflicts.Count);

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

    public string SchoolAiProviderId
    {
        get => _schoolAiProviderId;
        set
        {
            string next = string.IsNullOrWhiteSpace(value) ? "openai" : value.Trim();
            if (_schoolAiProviderId != next)
            {
                _schoolAiProviderId = next;
                OnPropertyChanged();
            }
        }
    }

    public string SchoolAiKeyStatusText
    {
        get => _schoolAiKeyStatusText;
        private set
        {
            if (_schoolAiKeyStatusText != value)
            {
                _schoolAiKeyStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string SchoolAdminStatusText
    {
        get => _schoolAdminStatusText;
        private set
        {
            if (_schoolAdminStatusText != value)
            {
                _schoolAdminStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string EnrollmentDisplayName
    {
        get => _enrollmentDisplayName;
        set
        {
            string next = value ?? string.Empty;
            if (_enrollmentDisplayName != next)
            {
                _enrollmentDisplayName = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEnrollProfile));
            }
        }
    }

    public string EnrollmentRole
    {
        get => _enrollmentRole;
        set
        {
            string next = string.IsNullOrWhiteSpace(value) ? "student" : value.Trim();
            if (_enrollmentRole != next)
            {
                _enrollmentRole = next;
                OnPropertyChanged();
            }
        }
    }

    public string EnrollmentBirthYearText
    {
        get => _enrollmentBirthYearText;
        set
        {
            string next = value ?? string.Empty;
            if (_enrollmentBirthYearText != next)
            {
                _enrollmentBirthYearText = next;
                OnPropertyChanged();
            }
        }
    }

    public string LastEnrollmentTokenText
    {
        get => _lastEnrollmentTokenText;
        private set
        {
            if (_lastEnrollmentTokenText != value)
            {
                _lastEnrollmentTokenText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastEnrollmentToken));
            }
        }
    }

    public bool HasLastEnrollmentToken => !string.IsNullOrWhiteSpace(LastEnrollmentTokenText);

    public string AiHistoryPurgeConfirmationText
    {
        get => _aiHistoryPurgeConfirmationText;
        set
        {
            string next = value ?? string.Empty;
            if (_aiHistoryPurgeConfirmationText != next)
            {
                _aiHistoryPurgeConfirmationText = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPurgeAiHistory));
            }
        }
    }

    public string SchoolAuditFilterText
    {
        get => _schoolAuditFilterText;
        set
        {
            string next = value ?? string.Empty;
            if (_schoolAuditFilterText != next)
            {
                _schoolAuditFilterText = next;
                OnPropertyChanged();
                ApplySchoolAuditFilter();
            }
        }
    }

    public int PerStudentDailyTokenBudget
    {
        get => _perStudentDailyTokenBudget;
        set
        {
            int next = Math.Max(0, value);
            if (_perStudentDailyTokenBudget != next)
            {
                _perStudentDailyTokenBudget = next;
                OnPropertyChanged();
            }
        }
    }

    public int ClassDailyTokenBudget
    {
        get => _classDailyTokenBudget;
        set
        {
            int next = Math.Max(0, value);
            if (_classDailyTokenBudget != next)
            {
                _classDailyTokenBudget = next;
                OnPropertyChanged();
            }
        }
    }

    public int PerStudentQueriesPerMinute
    {
        get => _perStudentQueriesPerMinute;
        set
        {
            int next = Math.Max(0, value);
            if (_perStudentQueriesPerMinute != next)
            {
                _perStudentQueriesPerMinute = next;
                OnPropertyChanged();
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
        LibraryHostState.Running => LocalizeFormat("Sharing.Host.RunningOn", "Running on :{0}", _status.Port),
        LibraryHostState.Starting => Localize("Sharing.Host.Starting", "Starting"),
        LibraryHostState.Error => _status.ErrorMessage ?? Localize("Sharing.Host.Error", "Error"),
        _ => Localize("Sharing.Host.Stopped", "Stopped"),
    };

    public string ClientCountText => LocalizeFormat("Sharing.Host.ClientCount", "{0} clients", _status.ConnectedClientCount);

    public string FingerprintText =>
        string.IsNullOrWhiteSpace(_status.CertificateFingerprint)
            ? Localize("Sharing.Host.NoFingerprint", "No fingerprint")
            : _status.CertificateFingerprint[..Math.Min(12, _status.CertificateFingerprint.Length)];

    public string FullFingerprintText =>
        string.IsNullOrWhiteSpace(_status.CertificateFingerprint)
            ? Localize("Sharing.Host.NoFingerprintAvailable", "No fingerprint available")
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

    public string SharePanelTitle => Localize("Sharing.Host.ShareTitle", _sharePanelTitle);

    public string SharePanelSubtitle => CanShare
        ? LocalizeFormat("Sharing.Host.ShareSubtitle", "Scan or copy the join link for {0}.", _settings.DisplayName)
        : Localize("Sharing.Host.StartBeforeSharing", "Start the Host before sharing.");

    public string EnrollmentCodeText => string.IsNullOrWhiteSpace(_status.EnrollmentCode)
        ? Localize("Sharing.Host.NoEnrollmentCode", "No enrollment code")
        : LocalizeFormat("Sharing.Host.EnrollmentCode", "Enrollment code: {0}", _status.EnrollmentCode);

    public string ShareButtonText => Localize("Sharing.Host.Share", _shareButtonText);

    public string CopyJoinLinkText => Localize("Sharing.Host.CopyJoinLink", _copyJoinLinkText);

    public string CopyFingerprintText => Localize("Sharing.Host.CopyFingerprint", _copyFingerprintText);

    public string CloseSharePanelText => Localize("Sharing.Close", _closeSharePanelText);

    public string ContentModeText => _settings.ContentMode == HostContentDeliveryMode.PageRender
        ? Localize("Sharing.Host.PageRender", "Page Render")
        : Localize("Sharing.Host.FileStream", "File Stream");

    public string ToggleContentModeText => _settings.ContentMode == HostContentDeliveryMode.PageRender
        ? Localize("Sharing.Host.UseFileStream", "Use File Stream")
        : Localize("Sharing.Host.UsePageRender", "Use Page Render");

    public string StartConfirmationText => Localize("Sharing.Host.StartConfirmation", _startConfirmationText);

    public string FileStreamConfirmationText => Localize("Sharing.Host.FileStreamConfirmation", _fileStreamConfirmationText);

    public string ConfirmStartText => Localize("Sharing.Host.Start", _confirmStartText);

    public string ConfirmFileStreamText => Localize("Sharing.Host.UseFileStream", _confirmFileStreamText);

    public string CancelConfirmationText => Localize("Sharing.Cancel", _cancelConfirmationText);

    public string PrimaryActionText => IsRunning
        ? Localize("Sharing.Host.Stop", "Stop")
        : Localize("Sharing.Host.Start", "Start");

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
                RaiseSchoolAdminCapabilitiesChanged();
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

    public bool HasSchoolAdministration =>
        _profileEnrollmentService is not null ||
        _schoolAiKeyProvider is not null ||
        _schoolAiPolicyService is not null ||
        _usageDashboardService is not null ||
        _schoolAiHistoryManagementService is not null ||
        _auditRepository is not null;

    public bool CanSaveSchoolAiKey => !IsBusy && _schoolAiKeyProvider is not null;

    public bool CanDeleteSchoolAiKey => !IsBusy && _schoolAiKeyProvider is not null;

    public bool CanTestSchoolAiKey => !IsBusy && _schoolAiKeyProvider is not null;

    public bool CanSaveSchoolAiPolicy => !IsBusy && _schoolAiPolicyService is not null;

    public bool CanPurgeAiHistory =>
        !IsBusy &&
        _schoolAiHistoryManagementService is not null &&
        string.Equals(AiHistoryPurgeConfirmationText.Trim(), "PURGE AI HISTORY", StringComparison.Ordinal);

    public bool CanExportSchoolAuditCsv => !IsBusy && SchoolAuditEvents.Count > 0;

    public bool CanEnrollProfile =>
        !IsBusy &&
        _profileEnrollmentService is not null &&
        !string.IsNullOrWhiteSpace(EnrollmentDisplayName);

    public bool CanRevokeSelectedProfile =>
        !IsBusy &&
        _profileEnrollmentService is not null &&
        SelectedEnrolledProfile is not null &&
        SelectedEnrolledProfile.Status == EnrollmentStatus.Active;

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
        await RefreshSchoolAdminAsync(cancellationToken).ConfigureAwait(false);
        RaiseStatusChanged();
    }

    public async Task RefreshSchoolAdminAsync(CancellationToken cancellationToken = default)
    {
        if (!HasSchoolAdministration)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await RefreshSchoolAiKeyStatusAsync(cancellationToken).ConfigureAwait(true);
            await RefreshSchoolAiPolicyAsync(cancellationToken).ConfigureAwait(true);
            await RefreshEnrolledProfilesAsync(cancellationToken).ConfigureAwait(true);
            await RefreshUsageDashboardAsync(cancellationToken).ConfigureAwait(true);
            await RefreshAuditEventsAsync(cancellationToken).ConfigureAwait(true);
            SchoolAdminStatusText = Localize("Sharing.SchoolAdmin.Loaded", "School administration loaded");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            SchoolAdminStatusText = LocalizeFormat("Sharing.SchoolAdmin.LoadFailed", "School administration failed: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task SaveSchoolAiKeyAsync(char[] key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_schoolAiKeyProvider is null)
        {
            Array.Clear(key);
            return;
        }

        IsBusy = true;
        try
        {
            if (key.Length == 0 || key.All(static ch => char.IsWhiteSpace(ch)))
            {
                SchoolAiKeyStatusText = Localize("Sharing.SchoolAi.EnterKey", "Enter a key before saving");
                return;
            }

            await _schoolAiKeyProvider.SaveKeyAsync(SchoolAiProviderId, key, cancellationToken)
                .ConfigureAwait(true);
            SchoolAiKeyStatusText = LocalizeFormat("Sharing.SchoolAi.KeySaved", "Key saved for {0}", SchoolAiProviderId);
            await RefreshSchoolAiKeyStatusAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Array.Clear(key);
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task DeleteSchoolAiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolAiKeyProvider is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolAiKeyProvider.DeleteKeyAsync(SchoolAiProviderId, cancellationToken)
                .ConfigureAwait(true);
            SchoolAiKeyStatusText = LocalizeFormat("Sharing.SchoolAi.KeyRemoved", "Key removed for {0}", SchoolAiProviderId);
            await RefreshSchoolAiKeyStatusAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task TestSchoolAiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolAiKeyProvider is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            SchoolAiKeyStatus status = await _schoolAiKeyProvider
                .GetStatusAsync(SchoolAiProviderId, cancellationToken)
                .ConfigureAwait(true);
            SchoolAiKeyStatusText = status.IsConfigured
                ? LocalizeFormat("Sharing.SchoolAi.KeyTestPassed", "Key test passed for {0}: key is configured", status.ProviderId)
                : LocalizeFormat("Sharing.SchoolAi.KeyTestFailed", "Key test failed for {0}: no key configured", status.ProviderId);
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task SaveSchoolAiPolicyAsync(CancellationToken cancellationToken = default)
    {
        if (_schoolAiPolicyService is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolAiPolicyService.SavePolicyAsync(
                    new SchoolAiPolicy(
                        AiPrivacyTier.MetadataOnly,
                        ContentAwareEnabled: false,
                        PerStudentDailyTokenBudget,
                        ClassDailyTokenBudget,
                        PerStudentQueriesPerMinute,
                        AnswerModeEnabled: false),
                    cancellationToken)
                .ConfigureAwait(true);
            SchoolAdminStatusText = Localize("Sharing.SchoolAdmin.PolicySaved", "School AI policy saved");
            await RefreshUsageDashboardAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task EnrollProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!CanEnrollProfile || _profileEnrollmentService is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            int? birthYear = ParseOptionalBirthYear(EnrollmentBirthYearText);
            EnrollmentToken token = await _profileEnrollmentService
                .EnrollAsync(
                    new EnrollProfileRequest(
                        EnrollmentDisplayName.Trim(),
                        EnrollmentRole.Trim(),
                        birthYear),
                    cancellationToken)
                .ConfigureAwait(true);
            LastEnrollmentTokenText = LocalizeFormat(
                "Sharing.SchoolAdmin.EnrollmentToken",
                "Profile {0:D} token {1} expires {2:u}",
                token.ProfileId,
                token.Token,
                token.ExpiresUtc);
            EnrollmentDisplayName = string.Empty;
            await RefreshEnrolledProfilesAsync(cancellationToken).ConfigureAwait(true);
            await RefreshUsageDashboardAsync(cancellationToken).ConfigureAwait(true);
            SchoolAdminStatusText = Localize("Sharing.SchoolAdmin.ProfileEnrolled", "Profile enrolled");
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task RevokeSelectedProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRevokeSelectedProfile || _profileEnrollmentService is null || SelectedEnrolledProfile is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Guid revokedProfileId = SelectedEnrolledProfile.ProfileId;
            await _profileEnrollmentService.RevokeAsync(revokedProfileId, cancellationToken)
                .ConfigureAwait(true);
            await RefreshEnrolledProfilesAsync(cancellationToken).ConfigureAwait(true);
            SchoolAdminStatusText = Localize("Sharing.SchoolAdmin.ProfileRevoked", "Profile revoked");
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task PurgeAiHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (!CanPurgeAiHistory || _schoolAiHistoryManagementService is null)
        {
            SchoolAdminStatusText = Localize("Sharing.SchoolAdmin.PurgeConfirmation", "Type PURGE AI HISTORY before clearing school AI history.");
            return;
        }

        IsBusy = true;
        try
        {
            SchoolAiHistoryPurgeResult result = await _schoolAiHistoryManagementService
                .PurgeInstitutionHistoryAsync(cancellationToken)
                .ConfigureAwait(true);
            AiHistoryPurgeConfirmationText = string.Empty;
            await RefreshUsageDashboardAsync(cancellationToken).ConfigureAwait(true);
            await RefreshAuditEventsAsync(cancellationToken).ConfigureAwait(true);
            SchoolAdminStatusText = LocalizeFormat(
                "Sharing.SchoolAdmin.PurgedHistory",
                "Purged {0:N0} AI history rows and {1:N0} usage rows",
                result.QueryHistoryRowsDeleted,
                result.UsageLedgerRowsDeleted);
        }
        finally
        {
            IsBusy = false;
            RaiseSchoolAdminCapabilitiesChanged();
        }
    }

    public async Task ExportSchoolAuditCsvAsync(Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync("timestampUtc,actorId,eventType,entityId,payload").ConfigureAwait(false);
        foreach (SchoolAuditRow row in SchoolAuditEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(
                    ",",
                    EscapeCsv(row.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.ActorId),
                    EscapeCsv(row.EventType),
                    EscapeCsv(row.EntityId),
                    EscapeCsv(row.Payload)))
                .ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        SchoolAdminStatusText = LocalizeFormat(
            "Sharing.SchoolAdmin.AuditExported",
            "Exported {0:N0} school audit rows to CSV",
            SchoolAuditEvents.Count);
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
                ClientConnectionStatusText = errorMessage ?? Localize("Sharing.Client.InvalidJoinLink", "Join link is invalid.");
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
                    ? LocalizeFormat("Sharing.Client.ConnectedTo", "Connected to {0}", displayName)
                    : Localize("Sharing.Client.ConnectedToClassroom", "Connected to classroom Host");
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
                HostTrustState.FirstUse => Localize("Sharing.Client.TrustFingerprint", "Trust this Host fingerprint before connecting."),
                HostTrustState.Mismatch => Localize("Sharing.Client.FingerprintMismatch", "Host fingerprint does not match the trusted pin."),
                _ => Localize("Sharing.Client.ConnectionUnavailable", "Could not connect to classroom Host."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            ClientConnectionStatusText = LocalizeFormat("Sharing.Client.ConnectionFailed", "Connection failed: {0}", ex.Message);
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
            ClientConnectionStatusText = Localize("Sharing.Client.Scanning", "Scanning for classroom Hosts");
            IReadOnlyList<DiscoveredClassroomHost> hosts = await _mdnsResolver
                .DiscoverAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(true);
            DiscoveredHosts = hosts;
            SelectedDiscoveredHost = hosts.Count == 1 ? hosts[0] : null;
            ClientConnectionStatusText = hosts.Count switch
            {
                0 => Localize("Sharing.Client.NoHosts", "No classroom Hosts found"),
                1 => LocalizeFormat("Sharing.Client.SelectedHost", "Selected {0}", hosts[0].DisplayName),
                _ => LocalizeFormat("Sharing.Client.HostCount", "{0} classroom Hosts found", hosts.Count),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            ClientConnectionStatusText = LocalizeFormat("Sharing.Client.ScanFailed", "Host scan failed: {0}", ex.Message);
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

    public void MarkJoinLinkCopied() => ShareConfirmationText = Localize("Sharing.Host.JoinLinkCopied", "Join link copied to clipboard");

    public void MarkFingerprintCopied() => ShareConfirmationText = Localize("Sharing.Host.FingerprintCopied", "Fingerprint copied to clipboard");

    public void ReportClipboardUnavailable() => ShareConfirmationText = Localize("Sharing.Host.ClipboardUnavailable", "Clipboard is unavailable");

    private async Task RunSyncNowAsync(CancellationToken cancellationToken)
    {
        if (_syncService is null)
        {
            return;
        }

        try
        {
            SyncStatusText = Localize("Sharing.Sync.Syncing", "Syncing");
            ClassroomSyncStatus status = await _syncService.SyncNowAsync(cancellationToken).ConfigureAwait(true);
            ApplySyncStatus(status);
            await RefreshAnnotationConflictsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            SyncStatusText = LocalizeFormat("Sharing.Sync.Failed", "Sync failed: {0}", ex.Message);
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
            SyncStatusText = Localize("Sharing.Sync.Unavailable", "Sync unavailable");
            PendingAnnotationConflicts = [];
            SelectedAnnotationConflict = null;
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        if (_classroomModeService is not null && !IsSyncOptInEnabled)
        {
            _isSyncEnabled = false;
            SyncStatusText = Localize("Sharing.Sync.PrivateOff", "Private sync is off");
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
            SyncStatusText = LocalizeFormat("Sharing.Sync.ConflictChoiceFailed", "Conflict choice failed: {0}", ex.Message);
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

    private async Task RefreshSchoolAiKeyStatusAsync(CancellationToken cancellationToken)
    {
        if (_schoolAiKeyProvider is null)
        {
            return;
        }

        SchoolAiKeyStatus status = await _schoolAiKeyProvider
            .GetStatusAsync(SchoolAiProviderId, cancellationToken)
            .ConfigureAwait(true);
        SchoolAiKeyStatusText = status.IsConfigured
            ? LocalizeFormat("Sharing.SchoolAi.KeyConfigured", "Key configured for {0}", status.ProviderId)
            : LocalizeFormat("Sharing.SchoolAi.NoKeyConfigured", "No key configured for {0}", status.ProviderId);
    }

    private async Task RefreshSchoolAiPolicyAsync(CancellationToken cancellationToken)
    {
        if (_schoolAiPolicyService is null)
        {
            return;
        }

        SchoolAiPolicy policy = await _schoolAiPolicyService.GetPolicyAsync(cancellationToken)
            .ConfigureAwait(true);
        PerStudentDailyTokenBudget = policy.PerStudentDailyTokenBudget;
        ClassDailyTokenBudget = policy.ClassDailyTokenBudget;
        PerStudentQueriesPerMinute = policy.PerStudentQueriesPerMinute;
    }

    private async Task RefreshEnrolledProfilesAsync(CancellationToken cancellationToken)
    {
        if (_profileEnrollmentService is null)
        {
            return;
        }

        Guid? selectedId = SelectedEnrolledProfile?.ProfileId;
        IReadOnlyList<EnrolledProfile> profiles = await _profileEnrollmentService
            .ListAsync(cancellationToken)
            .ConfigureAwait(true);
        EnrolledProfiles.Clear();
        foreach (EnrolledProfile profile in profiles)
        {
            EnrolledProfiles.Add(profile);
        }

        SelectedEnrolledProfile = selectedId is Guid id
            ? EnrolledProfiles.FirstOrDefault(profile => profile.ProfileId == id)
            : EnrolledProfiles.FirstOrDefault();
    }

    private async Task RefreshUsageDashboardAsync(CancellationToken cancellationToken)
    {
        if (_usageDashboardService is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IReadOnlyList<UsageDashboardEntry> entries = await _usageDashboardService
            .GetSummaryAsync(now.AddDays(-7), now, cancellationToken)
            .ConfigureAwait(true);
        SchoolAiUsage.Clear();
        foreach (UsageDashboardEntry entry in entries)
        {
            SchoolAiUsage.Add(entry);
        }
    }

    private async Task RefreshAuditEventsAsync(CancellationToken cancellationToken)
    {
        if (_auditRepository is null)
        {
            return;
        }

        IReadOnlyList<AuditEvent> events = await _auditRepository
            .ReadRecentAsync(25, cancellationToken)
            .ConfigureAwait(true);
        _allSchoolAuditEvents = events
            .Select(auditEvent => new SchoolAuditRow(
                auditEvent.TimestampUtc,
                auditEvent.ActorId ?? "unknown",
                auditEvent.EventType,
                auditEvent.EntityId ?? string.Empty,
                auditEvent.Payload ?? string.Empty))
            .ToList();
        ApplySchoolAuditFilter();
    }

    private void ApplySchoolAuditFilter()
    {
        string filter = SchoolAuditFilterText.Trim();
        IEnumerable<SchoolAuditRow> rows = _allSchoolAuditEvents;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            rows = rows.Where(row =>
                row.ActorId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.EventType.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.EntityId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.Payload.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        SchoolAuditEvents.Clear();
        foreach (SchoolAuditRow row in rows)
        {
            SchoolAuditEvents.Add(row);
        }

        OnPropertyChanged(nameof(CanExportSchoolAuditCsv));
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
                ? Localize("Sharing.Sync.Unavailable", "Sync unavailable")
                : LocalizeFormat("Sharing.Sync.UnavailableWithReason", "Sync unavailable: {0}", status.ErrorMessage);
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        _isSyncEnabled = true;
        if (status.IsRunning)
        {
            SyncStatusText = Localize("Sharing.Sync.Syncing", "Syncing");
            OnPropertyChanged(nameof(CanSyncNow));
            return;
        }

        string conflictText = status.ConflictCount == 1
            ? Localize("Sharing.Sync.OneConflict", "1 conflict")
            : LocalizeFormat("Sharing.Sync.ManyConflicts", "{0} conflicts", status.ConflictCount);
        SyncStatusText = status.LastSyncedUtc is null
            ? LocalizeFormat("Sharing.Sync.Ready", "Ready to sync, {0}", conflictText)
            : LocalizeFormat(
                "Sharing.Sync.LastSynced",
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

    private void RaiseSchoolAdminCapabilitiesChanged()
    {
        OnPropertyChanged(nameof(CanSaveSchoolAiKey));
        OnPropertyChanged(nameof(CanDeleteSchoolAiKey));
        OnPropertyChanged(nameof(CanTestSchoolAiKey));
        OnPropertyChanged(nameof(CanSaveSchoolAiPolicy));
        OnPropertyChanged(nameof(CanEnrollProfile));
        OnPropertyChanged(nameof(CanRevokeSelectedProfile));
        OnPropertyChanged(nameof(CanPurgeAiHistory));
        OnPropertyChanged(nameof(CanExportSchoolAuditCsv));
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

    private static string EscapeCsv(string? value)
    {
        string text = value ?? string.Empty;
        return text.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? text
            : $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string FormatAnnotationConflict(StudentAnnotationConflict conflict) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}, page {1}: local \"{2}\" / server \"{3}\"",
            conflict.LocalAnnotation.BookId,
            conflict.LocalAnnotation.PageNumber,
            TruncateConflictText(conflict.LocalAnnotation.Body),
            TruncateConflictText(conflict.RemoteAnnotation.Body));

    private static int? ParseOptionalBirthYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int birthYear))
        {
            throw new ArgumentException("Birth year must be a four-digit year.", nameof(value));
        }

        if (birthYear is < 1900 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), birthYear, "Birth year is outside the supported range.");
        }

        return birthYear;
    }

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

    private string Localize(string key, string fallback) =>
        _localization is null ? fallback : _localization[key];

    private string LocalizeFormat(string key, string fallback, params object[] arguments)
    {
        string format = _localization is null ? fallback : _localization[key];
        return string.Format(CultureInfo.CurrentCulture, format, arguments);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SchoolAdminTitle));
        OnPropertyChanged(nameof(ClientTitle));
        OnPropertyChanged(nameof(DiscoveredHostsLabel));
        OnPropertyChanged(nameof(RefreshHostsText));
        OnPropertyChanged(nameof(SyncNowText));
        OnPropertyChanged(nameof(SyncOptInText));
        OnPropertyChanged(nameof(SyncOnReconnectText));
        OnPropertyChanged(nameof(SyncConflictsLabel));
        OnPropertyChanged(nameof(KeepLocalText));
        OnPropertyChanged(nameof(KeepServerText));
        OnPropertyChanged(nameof(JoinLinkLabel));
        OnPropertyChanged(nameof(SavedProfileLabel));
        OnPropertyChanged(nameof(ProfileDisplayNameLabel));
        OnPropertyChanged(nameof(GuestProfileText));
        OnPropertyChanged(nameof(AcceptTrustText));
        OnPropertyChanged(nameof(ConnectToHostText));
        OnPropertyChanged(nameof(SharePanelTitle));
        OnPropertyChanged(nameof(ShareButtonText));
        OnPropertyChanged(nameof(CopyJoinLinkText));
        OnPropertyChanged(nameof(CopyFingerprintText));
        OnPropertyChanged(nameof(CloseSharePanelText));
        OnPropertyChanged(nameof(StartConfirmationText));
        OnPropertyChanged(nameof(FileStreamConfirmationText));
        OnPropertyChanged(nameof(ConfirmStartText));
        OnPropertyChanged(nameof(ConfirmFileStreamText));
        OnPropertyChanged(nameof(CancelConfirmationText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(PendingConflictCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ClientCountText));
        OnPropertyChanged(nameof(FingerprintText));
        OnPropertyChanged(nameof(FullFingerprintText));
        OnPropertyChanged(nameof(SharePanelSubtitle));
        OnPropertyChanged(nameof(EnrollmentCodeText));
        OnPropertyChanged(nameof(ContentModeText));
        OnPropertyChanged(nameof(ToggleContentModeText));
        OnPropertyChanged(nameof(ClientConnectionStatusText));
        OnPropertyChanged(nameof(SyncStatusText));
        OnPropertyChanged(nameof(SchoolAiKeyStatusText));
        OnPropertyChanged(nameof(SchoolAdminStatusText));
    }

    /// <summary>Releases the optional localization subscription.</summary>
    public void Dispose()
    {
        if (_localization is not null)
        {
            _localization.CultureChanged -= OnCultureChanged;
        }
    }
}

public sealed record SchoolAuditRow(
    DateTimeOffset TimestampUtc,
    string ActorId,
    string EventType,
    string EntityId,
    string Payload);
