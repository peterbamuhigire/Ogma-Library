using System.Text;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.LanHost;

public sealed class HostSharingViewModelTests
{
    [Fact]
    public void HostSharingViewModel_LocalizesStaticControlLabelsAndRefreshesOnCultureChange()
    {
        var localization = new OgmaLibrary.Infrastructure.Localization.InMemoryLocalizationService();
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            localization: localization);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Equal("Host", viewModel.Title);
        Assert.Equal("Share", viewModel.ShareButtonText);
        Assert.Equal("Start", viewModel.ConfirmStartText);

        localization.SetCulture("fr");

        Assert.Equal("Hote", viewModel.Title);
        Assert.Equal("Partager", viewModel.ShareButtonText);
        Assert.Equal("Demarrer", viewModel.ConfirmStartText);
        Assert.Contains(nameof(HostSharingViewModel.Title), changed);
        Assert.Contains(nameof(HostSharingViewModel.ShareButtonText), changed);
        Assert.Contains(nameof(HostSharingViewModel.ConfirmStartText), changed);

        viewModel.Dispose();
    }

    [Fact]
    public async Task HostSharingViewModel_LocalizesRuntimeStatusCopy()
    {
        var localization = new OgmaLibrary.Infrastructure.Localization.InMemoryLocalizationService();
        localization.SetCulture("fr");
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            localization: localization);

        await viewModel.StartAsync();

        Assert.Equal("En cours sur :7473", viewModel.StatusText);
        Assert.Equal("0 clients", viewModel.ClientCountText);
        Assert.Equal("Rendu de page", viewModel.ContentModeText);
        Assert.Equal("Arreter", viewModel.PrimaryActionText);

        viewModel.Dispose();
    }

    [Fact]
    public async Task HostSharingViewModel_StartAndStop_UpdateControlState()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        await viewModel.ToggleContentModeAsync();

        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);
        Assert.Equal("File Stream", viewModel.ContentModeText);
        Assert.Equal("Use Page Render", viewModel.ToggleContentModeText);

        await viewModel.StartAsync();

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.CanStart);
        Assert.True(viewModel.CanStop);
        Assert.False(viewModel.CanChangeContentMode);
        Assert.True(viewModel.CanShare);
        Assert.Equal("Running on :7473", viewModel.StatusText);
        Assert.Equal("0123456789ab", viewModel.FingerprintText);

        await viewModel.ToggleContentModeAsync();

        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);

        await viewModel.StopAsync();

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.CanStart);
        Assert.False(viewModel.CanStop);
        Assert.Equal("Stopped", viewModel.StatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_RiskyActions_RequireExplicitConfirmation()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        viewModel.RequestStartConfirmation();

        Assert.True(viewModel.IsStartConfirmationOpen);
        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.CanStart);

        viewModel.CancelStartConfirmation();

        Assert.False(viewModel.IsStartConfirmationOpen);
        Assert.True(viewModel.CanStart);

        viewModel.RequestStartConfirmation();
        await viewModel.ConfirmStartAsync();

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.IsStartConfirmationOpen);

        await viewModel.StopAsync();
        await viewModel.RequestContentModeChangeAsync();

        Assert.True(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.PageRender, settings.Settings.ContentMode);

        viewModel.CancelFileStreamConfirmation();

        Assert.False(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.PageRender, settings.Settings.ContentMode);

        await viewModel.RequestContentModeChangeAsync();
        await viewModel.ConfirmFileStreamAsync();

        Assert.False(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);
    }

    [Fact]
    public async Task HostSharingViewModel_SharePanel_BuildsQrJoinPayloadAndCopyConfirmations()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        Assert.False(viewModel.CanShare);
        Assert.False(viewModel.IsSharePanelOpen);

        await viewModel.StartAsync();
        viewModel.OpenSharePanel();

        Assert.True(viewModel.IsSharePanelOpen);
        Assert.Contains("ogma-lan://127.0.0.1:7473/join", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("name=Ogma%20Test%20Host", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("fp=0123456789abcdef", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("code=ABCD2345", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("auth=enrollment-code", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Equal("Enrollment code: ABCD2345", viewModel.EnrollmentCodeText);
        Assert.Contains("\u2588", viewModel.QrCodeText, StringComparison.Ordinal);
        Assert.Contains("0123 4567 89AB CDEF", viewModel.FullFingerprintText, StringComparison.Ordinal);

        viewModel.MarkJoinLinkCopied();

        Assert.True(viewModel.HasShareConfirmation);
        Assert.Equal("Join link copied to clipboard", viewModel.ShareConfirmationText);

        viewModel.MarkFingerprintCopied();

        Assert.Equal("Fingerprint copied to clipboard", viewModel.ShareConfirmationText);

        await viewModel.StopAsync();

        Assert.False(viewModel.CanShare);
        Assert.False(viewModel.IsSharePanelOpen);
    }

    [Fact]
    public async Task HostSharingViewModel_ConnectToHost_ParsesJoinLinkAndCallsConnectionService()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var connection = new RecordingConnectionService();
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            new ClassroomJoinParser(),
            connection)
        {
            JoinLink = $"ogma-lan://127.0.0.1:7473/join?name=School&fp={FakeLibraryHostService.Fingerprint}&code=ABCD2345",
            ProfileDisplayName = "  Amina  ",
            AcceptFirstUseTrust = true,
        };

        await viewModel.ConnectToHostAsync();

        Assert.Equal(1, connection.ConnectCalls);
        Assert.True(connection.Request!.AcceptFirstUseTrust);
        Assert.Equal("Amina", connection.Request.ProfileDisplayName);
        Assert.False(connection.Request.UseGuestProfile);
        Assert.Equal("127.0.0.1", connection.Request.JoinRequest.Address);
        Assert.Equal("ABCD2345", connection.Request.JoinRequest.EnrollmentCode);
        Assert.Equal("Connected to School", viewModel.ClientConnectionStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_ProfilePicker_UsesSelectedPersistentProfile()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var connection = new RecordingConnectionService();
        var profile = new ClassroomProfile(
            Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
            "Amina",
            ClassroomRole.Student,
            IsGuest: false);
        var profileService = new RecordingProfileService(profile);
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            new ClassroomJoinParser(),
            connection,
            profileService: profileService)
        {
            JoinLink = $"ogma-lan://127.0.0.1:7473/join?name=School&fp={FakeLibraryHostService.Fingerprint}&code=ABCD2345",
            AcceptFirstUseTrust = true,
        };

        await viewModel.RefreshAsync();
        await viewModel.ConnectToHostAsync();

        Assert.True(viewModel.HasProfileManagement);
        Assert.True(viewModel.HasClassroomProfiles);
        Assert.Equal(profile, viewModel.SelectedClassroomProfile);
        Assert.Equal("Amina", viewModel.ProfileDisplayName);
        Assert.Equal(profile.ProfileId, connection.Request!.ProfileId);
        Assert.Equal("Amina", connection.Request.ProfileDisplayName);
        Assert.False(connection.Request.UseGuestProfile);
    }

    [Fact]
    public async Task HostSharingViewModel_ConnectToHost_InvalidJoinLinkShowsStatus()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var connection = new RecordingConnectionService();
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            new ClassroomJoinParser(),
            connection)
        {
            JoinLink = "not-a-join-link",
            ProfileDisplayName = "Amina",
        };

        await viewModel.ConnectToHostAsync();

        Assert.Equal(0, connection.ConnectCalls);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ClientConnectionStatusText));
        Assert.NotEqual("Not connected", viewModel.ClientConnectionStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_DiscoverHosts_SelectsSingleHostAndBuildsJoinLink()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var discovered = new DiscoveredClassroomHost(
            "room-12",
            "Room 12 Library",
            "192.168.1.13",
            7473,
            FakeLibraryHostService.Fingerprint,
            new Dictionary<string, string>
            {
                ["requires-auth"] = "true",
            });
        var resolver = new RecordingMdnsResolver(discovered);
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            mdnsResolver: resolver)
        {
            UseGuestProfile = true,
        };

        await viewModel.DiscoverHostsAsync();

        Assert.Equal(1, resolver.DiscoverCalls);
        Assert.True(viewModel.HasDiscoveredHosts);
        Assert.True(viewModel.CanDiscoverHosts);
        Assert.Equal(discovered, viewModel.SelectedDiscoveredHost);
        Assert.Contains("ogma-lan://192.168.1.13:7473/join", viewModel.JoinLink, StringComparison.Ordinal);
        Assert.Contains("name=Room%2012%20Library", viewModel.JoinLink, StringComparison.Ordinal);
        Assert.Contains($"fp={FakeLibraryHostService.Fingerprint}", viewModel.JoinLink, StringComparison.Ordinal);
        Assert.DoesNotContain("&code=", viewModel.JoinLink, StringComparison.Ordinal);
        Assert.Equal("Selected Room 12 Library", viewModel.ClientConnectionStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_ConnectToHost_SyncOnReconnectRunsAfterSuccessfulConnection()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var connection = new RecordingConnectionService();
        var sync = new RecordingSyncService
        {
            Status = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: null,
                ConflictCount: 0,
                ErrorMessage: null),
            NextStatus = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                ConflictCount: 0,
                ErrorMessage: null),
        };
        var mode = new RecordingClassroomModeService();
        await mode.SaveSyncSettingsAsync(new ClassroomSyncSettings(
            IsEnabled: true,
            SyncOnReconnect: true));
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            new ClassroomJoinParser(),
            connection,
            sync,
            mode)
        {
            JoinLink = $"ogma-lan://127.0.0.1:7473/join?name=School&fp={FakeLibraryHostService.Fingerprint}&code=ABCD2345",
            ProfileDisplayName = "Amina",
        };

        await viewModel.ConnectToHostAsync();

        Assert.Equal(1, connection.ConnectCalls);
        Assert.Equal(1, sync.SyncCalls);
        Assert.Equal("Connected to School", viewModel.ClientConnectionStatusText);
        Assert.Equal("Last synced 2026-06-02 16:00 UTC, 0 conflicts", viewModel.SyncStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_SyncNow_ReportsStatusAndCallsSyncService()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var sync = new RecordingSyncService
        {
            Status = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: null,
                ConflictCount: 1,
                ErrorMessage: null),
            NextStatus = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: new DateTimeOffset(2026, 6, 2, 15, 30, 0, TimeSpan.Zero),
                ConflictCount: 0,
                ErrorMessage: null),
        };
        var viewModel = new HostSharingViewModel(host, settings, syncService: sync);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.CanSyncNow);
        Assert.Equal("Ready to sync, 1 conflict", viewModel.SyncStatusText);

        await viewModel.SyncNowAsync();

        Assert.Equal(1, sync.SyncCalls);
        Assert.True(viewModel.CanSyncNow);
        Assert.Equal("Last synced 2026-06-02 15:30 UTC, 0 conflicts", viewModel.SyncStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_SyncOptIn_PersistsAndControlsSyncNow()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var sync = new RecordingSyncService
        {
            Status = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: null,
                ConflictCount: 0,
                ErrorMessage: null),
        };
        var mode = new RecordingClassroomModeService();
        var viewModel = new HostSharingViewModel(
            host,
            settings,
            syncService: sync,
            classroomModeService: mode);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.IsSyncOptInEnabled);
        Assert.False(viewModel.SyncOnReconnect);
        Assert.False(viewModel.CanSyncNow);
        Assert.False(viewModel.CanSyncOnReconnect);
        Assert.Equal("Private sync is off", viewModel.SyncStatusText);

        viewModel.IsSyncOptInEnabled = true;
        viewModel.SyncOnReconnect = true;
        await viewModel.SaveSyncSettingsAsync();

        Assert.True(mode.SyncSettings.IsEnabled);
        Assert.True(mode.SyncSettings.SyncOnReconnect);
        Assert.True(viewModel.CanSyncNow);
        Assert.True(viewModel.CanSyncOnReconnect);
        Assert.Equal("Ready to sync, 0 conflicts", viewModel.SyncStatusText);

        viewModel.IsSyncOptInEnabled = false;
        await viewModel.SaveSyncSettingsAsync();

        Assert.False(mode.SyncSettings.IsEnabled);
        Assert.False(mode.SyncSettings.SyncOnReconnect);
        Assert.False(viewModel.SyncOnReconnect);
        Assert.False(viewModel.CanSyncNow);
        Assert.Equal("Private sync is off", viewModel.SyncStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_AnnotationConflictChoice_RefreshesPendingList()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var sync = new RecordingSyncService
        {
            Status = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: null,
                ConflictCount: 1,
                ErrorMessage: null),
            NextStatus = new ClassroomSyncStatus(
                IsEnabled: true,
                IsRunning: false,
                LastSyncedUtc: new DateTimeOffset(2026, 6, 2, 15, 45, 0, TimeSpan.Zero),
                ConflictCount: 0,
                ErrorMessage: null),
        };
        var local = new StudentAnnotation(
            "annotation-1",
            "host-1",
            "book-1",
            7,
            "Note",
            null,
            "Local note",
            new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero));
        sync.AddConflict(new StudentAnnotationConflict(
            "host-1",
            local,
            local with { Body = "Server note" },
            new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero)));
        var viewModel = new HostSharingViewModel(host, settings, syncService: sync);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasPendingAnnotationConflicts);
        Assert.True(viewModel.CanResolveAnnotationConflict);
        Assert.Contains("Local note", viewModel.SelectedConflictText, StringComparison.Ordinal);
        Assert.Contains("Server note", viewModel.SelectedConflictText, StringComparison.Ordinal);

        await viewModel.KeepServerAnnotationConflictAsync();

        Assert.Equal(1, sync.ResolveCalls);
        Assert.Equal(ClassroomSyncConflictResolution.KeepServer, sync.LastResolution);
        Assert.False(viewModel.HasPendingAnnotationConflicts);
        Assert.False(viewModel.CanResolveAnnotationConflict);
        Assert.Equal("Last synced 2026-06-02 15:45 UTC, 0 conflicts", viewModel.SyncStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_SchoolAdminRefresh_LoadsProfilesPolicyUsageAndAudit()
    {
        var enrollment = new RecordingProfileEnrollmentService();
        var keys = new RecordingSchoolAiKeyProvider(isConfigured: true);
        var policy = new RecordingSchoolAiPolicyService();
        var usage = new RecordingUsageDashboardService();
        var audit = new RecordingAuditRepository();
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            profileEnrollmentService: enrollment,
            schoolAiKeyProvider: keys,
            schoolAiPolicyService: policy,
            usageDashboardService: usage,
            auditRepository: audit);

        await viewModel.RefreshSchoolAdminAsync();

        Assert.True(viewModel.HasSchoolAdministration);
        Assert.Equal("Key configured for openai", viewModel.SchoolAiKeyStatusText);
        Assert.Equal(250, viewModel.PerStudentDailyTokenBudget);
        Assert.Equal(1_000, viewModel.ClassDailyTokenBudget);
        Assert.Equal(7, viewModel.PerStudentQueriesPerMinute);
        Assert.Single(viewModel.EnrolledProfiles);
        Assert.Single(viewModel.SchoolAiUsage);
        Assert.Equal(2, viewModel.SchoolAuditEvents.Count);
        Assert.Equal("School administration loaded", viewModel.SchoolAdminStatusText);
    }

    [Fact]
    public async Task HostSharingViewModel_SchoolAdminCommands_SaveKeyPolicyEnrollAndRevoke()
    {
        var enrollment = new RecordingProfileEnrollmentService();
        var keys = new RecordingSchoolAiKeyProvider(isConfigured: false);
        var policy = new RecordingSchoolAiPolicyService();
        var history = new RecordingSchoolAiHistoryManagementService();
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            profileEnrollmentService: enrollment,
            schoolAiKeyProvider: keys,
            schoolAiPolicyService: policy,
            usageDashboardService: new RecordingUsageDashboardService(),
            schoolAiHistoryManagementService: history,
            auditRepository: new RecordingAuditRepository())
        {
            EnrollmentDisplayName = "Okello Reader",
            EnrollmentRole = "teacher",
            EnrollmentBirthYearText = "2010",
            PerStudentDailyTokenBudget = 500,
            ClassDailyTokenBudget = 5_000,
            PerStudentQueriesPerMinute = 3,
        };
        char[] key = "sk-test-value".ToCharArray();

        await viewModel.SaveSchoolAiKeyAsync(key);
        await viewModel.TestSchoolAiKeyAsync();
        await viewModel.SaveSchoolAiPolicyAsync();
        await viewModel.EnrollProfileAsync();
        viewModel.SelectedEnrolledProfile = viewModel.EnrolledProfiles.Single(profile => profile.DisplayName == "Okello Reader");
        await viewModel.RevokeSelectedProfileAsync();
        await viewModel.PurgeAiHistoryAsync();
        viewModel.AiHistoryPurgeConfirmationText = "PURGE AI HISTORY";
        await viewModel.PurgeAiHistoryAsync();

        Assert.True(key.All(ch => ch == '\0'));
        Assert.True(keys.IsConfigured);
        Assert.Equal("sk-test-value", keys.LastSavedKey);
        Assert.Contains("Key test passed", viewModel.SchoolAiKeyStatusText, StringComparison.Ordinal);
        Assert.Equal(500, policy.Policy.PerStudentDailyTokenBudget);
        Assert.Equal(5_000, policy.Policy.ClassDailyTokenBudget);
        Assert.Equal(3, policy.Policy.PerStudentQueriesPerMinute);
        Assert.Contains("token", viewModel.LastEnrollmentTokenText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(viewModel.EnrolledProfiles, profile =>
            profile.DisplayName == "Okello Reader" &&
            profile.Status == EnrollmentStatus.Revoked);
        Assert.Equal(1, history.PurgeCalls);
        Assert.Contains("Purged 2", viewModel.SchoolAdminStatusText, StringComparison.Ordinal);
        Assert.False(viewModel.CanPurgeAiHistory);
    }

    [Fact]
    public async Task HostSharingViewModel_AuditFilterAndCsvExport_UseVisibleRows()
    {
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            profileEnrollmentService: new RecordingProfileEnrollmentService(),
            auditRepository: new RecordingAuditRepository());
        await viewModel.RefreshSchoolAdminAsync();

        viewModel.SchoolAuditFilterText = "ai/search";
        using var stream = new MemoryStream();
        await viewModel.ExportSchoolAuditCsvAsync(stream);
        string csv = Encoding.UTF8.GetString(stream.ToArray());

        SchoolAuditRow row = Assert.Single(viewModel.SchoolAuditEvents);
        Assert.Equal("/api/v1/ai/search", row.EntityId);
        Assert.Contains("timestampUtc,actorId,eventType,entityId,payload", csv, StringComparison.Ordinal);
        Assert.Contains("/api/v1/ai/search", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/catalogue", csv, StringComparison.Ordinal);
        Assert.Contains("\"{\"\"action\"\":\"\"SearchSchoolAi\"\"}\"", csv, StringComparison.Ordinal);
        Assert.Contains("Exported 1", viewModel.SchoolAdminStatusText, StringComparison.Ordinal);
    }

    private sealed class FakeHostModeSettingsRepository : IHostModeSettingsRepository
    {
        public HostModeSettings Settings { get; private set; } = new(
            IsEnabled: false,
            Port: 7473,
            ContentMode: HostContentDeliveryMode.PageRender,
            DisplayName: "Ogma Test Host");

        public Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Settings);
        }

        public Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryHostService : ILibraryHostService
    {
        public const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private LibraryHostStatus _status = new(
            LibraryHostState.Stopped,
            Port: 7473,
            ConnectedClientCount: 0,
            CertificateFingerprint: null,
            ErrorMessage: null);

        public Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _status = new LibraryHostStatus(
                LibraryHostState.Running,
                Port: 7473,
                ConnectedClientCount: 0,
                CertificateFingerprint: Fingerprint,
                ErrorMessage: null,
                HostAddress: "127.0.0.1",
                EnrollmentCode: "ABCD2345");
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _status = _status with
            {
                State = LibraryHostState.Stopped,
                CertificateFingerprint = null,
                HostAddress = null,
                EnrollmentCode = null,
            };
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_status);
        }
    }

    private sealed class RecordingConnectionService : IClassroomConnectionService
    {
        public int ConnectCalls { get; private set; }

        public ClassroomConnectionRequest? Request { get; private set; }

        public Task<ClassroomConnectionResult> ConnectAsync(
            ClassroomConnectionRequest request,
            CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            Request = request;
            var connection = new ClassroomHostConnection(
                request.JoinRequest,
                "session-token",
                new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero));
            var profile = new ClassroomProfile(
                Guid.NewGuid(),
                request.ProfileDisplayName ?? "Guest",
                request.UseGuestProfile ? ClassroomRole.Guest : request.Role,
                request.UseGuestProfile);
            return Task.FromResult(new ClassroomConnectionResult(
                IsConnected: true,
                HostTrustState.Trusted,
                profile,
                connection));
        }
    }

    private sealed class RecordingSyncService : ISyncService
    {
        private readonly List<StudentAnnotationConflict> _conflicts = [];

        public ClassroomSyncStatus Status { get; init; } = new(
            IsEnabled: false,
            IsRunning: false,
            LastSyncedUtc: null,
            ConflictCount: 0,
            ErrorMessage: null);

        public ClassroomSyncStatus NextStatus { get; init; } = new(
            IsEnabled: true,
            IsRunning: false,
            LastSyncedUtc: null,
            ConflictCount: 0,
            ErrorMessage: null);

        public int SyncCalls { get; private set; }

        public int ResolveCalls { get; private set; }

        public ClassroomSyncConflictResolution? LastResolution { get; private set; }

        public void AddConflict(StudentAnnotationConflict conflict) => _conflicts.Add(conflict);

        public Task<ClassroomSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Status);
        }

        public Task<IReadOnlyList<StudentAnnotationConflict>> ListAnnotationConflictsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<StudentAnnotationConflict>>(_conflicts.ToArray());
        }

        public Task<ClassroomSyncStatus> ResolveAnnotationConflictAsync(
            string annotationId,
            ClassroomSyncConflictResolution resolution,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
            cancellationToken.ThrowIfCancellationRequested();
            ResolveCalls++;
            LastResolution = resolution;
            _conflicts.RemoveAll(conflict => conflict.LocalAnnotation.Id == annotationId);
            return Task.FromResult(NextStatus with { ConflictCount = _conflicts.Count });
        }

        public Task<ClassroomSyncStatus> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyncCalls++;
            return Task.FromResult(NextStatus);
        }
    }

    private sealed class RecordingClassroomModeService : IClassroomModeService
    {
        public ClassroomModeSettings Mode { get; private set; } = new(LibraryRuntimeMode.Standalone);

        public ClassroomSyncSettings SyncSettings { get; private set; } = new();

        public ClassroomConnectivityStatus ConnectivityStatus { get; private set; } = new(
            IsOnline: false,
            UpdatedUtc: DateTimeOffset.MinValue,
            Message: "Not connected");

        public IObservable<ClassroomConnectivityStatus> Connectivity { get; } =
            new EmptyObservable<ClassroomConnectivityStatus>();

        public Task<ClassroomModeSettings> GetModeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Mode);
        }

        public Task SaveModeAsync(ClassroomModeSettings settings, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Mode = settings;
            return Task.CompletedTask;
        }

        public Task<ClassroomSyncSettings> GetSyncSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SyncSettings);
        }

        public Task SaveSyncSettingsAsync(
            ClassroomSyncSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyncSettings = settings.IsEnabled ? settings : settings with { SyncOnReconnect = false };
            return Task.CompletedTask;
        }

        public Task<ClassroomConnectivityStatus> GetConnectivityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ConnectivityStatus);
        }

        public Task SetConnectivityAsync(
            ClassroomConnectivityStatus status,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectivityStatus = status;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyObservable<T> : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer) => new EmptySubscription();
    }

    private sealed class EmptySubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class RecordingMdnsResolver : IMdnsResolver
    {
        private readonly IReadOnlyList<DiscoveredClassroomHost> _hosts;

        public RecordingMdnsResolver(params DiscoveredClassroomHost[] hosts) => _hosts = hosts;

        public int DiscoverCalls { get; private set; }

        public IObservable<DiscoveredClassroomHost> Hosts { get; } =
            new EmptyObservable<DiscoveredClassroomHost>();

        public Task<IReadOnlyList<DiscoveredClassroomHost>> DiscoverAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverCalls++;
            return Task.FromResult(_hosts);
        }
    }

    private sealed class RecordingProfileService : IProfileService
    {
        private readonly List<ClassroomProfile> _profiles;
        private ClassroomProfile? _active;

        public RecordingProfileService(params ClassroomProfile[] profiles)
        {
            _profiles = profiles.ToList();
            _active = profiles.FirstOrDefault();
        }

        public Task<ClassroomProfile> CreateAsync(
            CreateClassroomProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = new ClassroomProfile(Guid.NewGuid(), request.DisplayName, request.Role, IsGuest: false);
            _profiles.Add(profile);
            _active = profile;
            return Task.FromResult(profile);
        }

        public Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _active = new ClassroomProfile(Guid.NewGuid(), "Guest", ClassroomRole.Guest, IsGuest: true);
            return Task.FromResult(_active);
        }

        public Task ClearGuestSessionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_active is { IsGuest: true })
            {
                _active = null;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ClassroomProfile>>(_profiles.ToArray());
        }

        public Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _active = _profiles.Single(profile => profile.ProfileId == profileId);
            return Task.CompletedTask;
        }

        public Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_active);
        }

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profiles.RemoveAll(profile => profile.ProfileId == profileId);
            if (_active?.ProfileId == profileId)
            {
                _active = null;
            }

            return Task.CompletedTask;
        }

        public Task StoreSessionTokenAsync(
            Guid profileId,
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }

        public Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProfileEnrollmentService : IProfileEnrollmentService
    {
        private readonly List<EnrolledProfile> _profiles =
        [
            new(
                Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                "Amina Reader",
                "student",
                EnrollmentStatus.Active,
                2014,
                new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero),
                RevokedUtc: null),
        ];

        public Task<EnrollmentToken> EnrollAsync(
            EnrollProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid profileId = Guid.NewGuid();
            _profiles.Add(new EnrolledProfile(
                profileId,
                request.DisplayName,
                request.Role,
                EnrollmentStatus.Active,
                request.BirthYear,
                DateTimeOffset.UtcNow,
                RevokedUtc: null));
            return Task.FromResult(new EnrollmentToken(
                profileId,
                "token-value",
                DateTimeOffset.UtcNow.AddHours(24)));
        }

        public Task<IReadOnlyList<EnrolledProfile>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<EnrolledProfile>>(_profiles.ToArray());
        }

        public Task RevokeAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _profiles.FindIndex(profile => profile.ProfileId == profileId);
            if (index >= 0)
            {
                _profiles[index] = _profiles[index] with
                {
                    Status = EnrollmentStatus.Revoked,
                    RevokedUtc = DateTimeOffset.UtcNow,
                };
            }

            return Task.CompletedTask;
        }

        public Task<EnrolledProfile?> RedeemTokenAsync(
            Guid profileId,
            string token,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<EnrolledProfile?>(_profiles.FirstOrDefault(profile => profile.ProfileId == profileId));
        }
    }

    private sealed class RecordingSchoolAiKeyProvider(bool isConfigured) : ISchoolAiKeyProvider
    {
        public bool IsConfigured { get; private set; } = isConfigured;

        public string? LastSavedKey { get; private set; }

        public Task SaveKeyAsync(string providerId, char[] key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSavedKey = new string(key);
            IsConfigured = true;
            Array.Clear(key);
            return Task.CompletedTask;
        }

        public Task<SchoolAiKeyStatus> GetStatusAsync(string providerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SchoolAiKeyStatus(providerId, IsConfigured, DateTimeOffset.UtcNow));
        }

        public Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConfigured = false;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSchoolAiPolicyService : ISchoolAiPolicyService
    {
        public SchoolAiPolicy Policy { get; private set; } = new(
            AiPrivacyTier.MetadataOnly,
            ContentAwareEnabled: false,
            PerStudentDailyTokenBudget: 250,
            ClassDailyTokenBudget: 1_000,
            PerStudentQueriesPerMinute: 7,
            AnswerModeEnabled: false);

        public Task<SchoolAiPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Policy);
        }

        public Task SavePolicyAsync(SchoolAiPolicy policy, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Policy = policy;
            return Task.CompletedTask;
        }

        public Task<SchoolAiQuotaDecision> CheckAndReserveQuotaAsync(
            Guid profileId,
            int estimatedTokens,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SchoolAiQuotaDecision(
                IsAllowed: true,
                RemainingStudentTokens: 100,
                RemainingClassTokens: 500,
                ResetUtc: DateTimeOffset.UtcNow.AddDays(1),
                Reason: null));
        }
    }

    private sealed class RecordingUsageDashboardService : IUsageDashboardService
    {
        public Task<IReadOnlyList<UsageDashboardEntry>> GetSummaryAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<UsageDashboardEntry>>(
            [
                new(
                    Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                    "Amina Reader",
                    QueryCount: 4,
                    TokensUsed: 120,
                    EstimatedCostUsd: 0.01m,
                    QuotaPercent: 12,
                    LastQueryUtc: DateTimeOffset.UtcNow),
            ]);
        }
    }

    private sealed class RecordingSchoolAiHistoryManagementService : ISchoolAiHistoryManagementService
    {
        public int PurgeCalls { get; private set; }

        public Task<SchoolAiHistoryPurgeResult> PurgeInstitutionHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurgeCalls++;
            return Task.FromResult(new SchoolAiHistoryPurgeResult(
                QueryHistoryRowsDeleted: 2,
                UsageLedgerRowsDeleted: 1,
                PurgedUtc: DateTimeOffset.UtcNow));
        }
    }

    private sealed class RecordingAuditRepository : IAuditRepository
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> ReadRecentAsync(int maxCount, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<AuditEvent>>(
            [
                new()
                {
                    Id = "audit-1",
                    EventType = "LanHostRequestServed",
                    EntityId = "/api/v1/ai/search",
                    ActorId = "client:aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Payload = "{\"action\":\"SearchSchoolAi\"}",
                },
                new()
                {
                    Id = "audit-2",
                    EventType = "LanHostRequestServed",
                    EntityId = "/api/v1/catalogue",
                    ActorId = "client:bbbbbbbb-cccc-4ddd-8eee-ffffffffffff",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Payload = "{\"action\":\"Catalogue\"}",
                },
            ]);
        }
    }
}
