using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.LanHost;

public sealed class HostSharingViewModelTests
{
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

        public Task<ClassroomSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Status);
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
}
