using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Client-mode Host connection orchestration tests.</summary>
public sealed class ClassroomConnectionServiceTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ChangedFingerprint = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public async Task ConnectionService_FirstUseWithoutAcceptance_DoesNotConnect()
    {
        using TestHarness harness = CreateHarness();

        ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(
            new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint, DisplayName: "School Library"),
            ProfileDisplayName: "Amina"));

        Assert.False(result.IsConnected);
        Assert.Equal(HostTrustState.FirstUse, result.TrustState);
        Assert.Equal(1, harness.HostClient.HealthCalls);
        Assert.Equal(0, harness.HostClient.SessionCalls);
        Assert.Null(await harness.ConnectionService.GetActiveAsync());
        Assert.Equal(LibraryRuntimeMode.Standalone, (await harness.ModeService.GetModeAsync()).Mode);
        Assert.False((await harness.ModeService.GetConnectivityAsync()).IsOnline);
    }

    [Fact]
    public async Task ConnectionService_AcceptsFirstUse_IssuesSessionAndSetsActiveConnection()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            TestHarness harness = CreateHarness(dataDirectory);
            var request = new ClassroomJoinRequest(
                "192.168.1.13",
                7473,
                Fingerprint,
                DisplayName: "School Library");

            ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(
                request,
                AcceptFirstUseTrust: true,
                ProfileDisplayName: " Amina "));

            Assert.True(result.IsConnected);
            Assert.Equal(HostTrustState.Trusted, result.TrustState);
            Assert.Equal("Amina", result.Profile!.DisplayName);
            Assert.False(result.Profile.IsGuest);
            Assert.Equal("issued-session-token", result.Connection!.SessionToken);
            Assert.Equal(request, result.Connection.Request);
            Assert.Equal(1, harness.HostClient.HealthCalls);
            Assert.Equal(1, harness.HostClient.SessionCalls);
            Assert.Equal(result.Profile.ProfileId, harness.HostClient.ProfileId);
            Assert.Equal(ClassroomRole.Student, harness.HostClient.Role);
            Assert.Equal(TimeSpan.FromHours(8), harness.HostClient.Lifetime);
            Assert.Equal("issued-session-token", await harness.ProfileService.GetSessionTokenAsync(result.Profile.ProfileId));

            ClassroomHostConnection? active = await harness.ConnectionService.GetActiveAsync();
            Assert.Equal("issued-session-token", active!.SessionToken);
            Assert.Equal(LibraryRuntimeMode.ConnectToHost, (await harness.ModeService.GetModeAsync()).Mode);
            ClassroomConnectivityStatus connectivity = await harness.ModeService.GetConnectivityAsync();
            Assert.True(connectivity.IsOnline);
            Assert.Equal("Connected to School Library", connectivity.Message);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ConnectionService_RejectsMismatchedFingerprint()
    {
        using TestHarness harness = CreateHarness();
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(
            request,
            AcceptFirstUseTrust: true,
            PresentedFingerprint: ChangedFingerprint,
            ProfileDisplayName: "Amina"));

        Assert.False(result.IsConnected);
        Assert.Equal(HostTrustState.Mismatch, result.TrustState);
        Assert.Equal(0, harness.HostClient.HealthCalls);
        Assert.Equal(0, harness.HostClient.SessionCalls);
        Assert.Null(await harness.ConnectionService.GetActiveAsync());
    }

    [Fact]
    public async Task ConnectionService_FetchesLiveFingerprintBeforeTrustEvaluation()
    {
        using TestHarness harness = CreateHarness();
        harness.HostClient.HealthFingerprint = ChangedFingerprint;
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(
            request,
            AcceptFirstUseTrust: true,
            ProfileDisplayName: "Amina"));

        Assert.False(result.IsConnected);
        Assert.Equal(HostTrustState.Mismatch, result.TrustState);
        Assert.Equal(1, harness.HostClient.HealthCalls);
        Assert.Equal(0, harness.HostClient.SessionCalls);
        Assert.Null(await harness.ConnectionService.GetActiveAsync());
    }

    [Fact]
    public async Task ConnectionService_UsesExistingActiveProfile()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            TestHarness harness = CreateHarness(dataDirectory);
            ClassroomProfile profile = await harness.ProfileService.CreateAsync(
                new CreateClassroomProfileRequest("Teacher Grace", ClassroomRole.Teacher));
            var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
            await harness.TrustService.AcceptAsync(request, Fingerprint);

            ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(request));

            Assert.True(result.IsConnected);
            Assert.Equal(profile.ProfileId, result.Profile!.ProfileId);
            Assert.Equal(1, harness.HostClient.HealthCalls);
            Assert.Equal(ClassroomRole.Teacher, harness.HostClient.Role);
            Assert.Equal("issued-session-token", await harness.ProfileService.GetSessionTokenAsync(profile.ProfileId));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ConnectionService_GuestProfile_DoesNotPersistSessionToken()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            TestHarness harness = CreateHarness(dataDirectory);
            var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

            ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(
                request,
                AcceptFirstUseTrust: true,
                UseGuestProfile: true));

            Assert.True(result.IsConnected);
            Assert.True(result.Profile!.IsGuest);
            Assert.Equal(ClassroomRole.Guest, result.Profile.Role);
            Assert.Equal(1, harness.HostClient.HealthCalls);
            Assert.Equal("issued-session-token", (await harness.ConnectionService.GetActiveAsync())!.SessionToken);
            Assert.Null(await harness.ProfileService.GetSessionTokenAsync(result.Profile.ProfileId));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ConnectionService_RequiresProfileWhenNoneCanBeResolved()
    {
        using TestHarness harness = CreateHarness();
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
        await harness.TrustService.AcceptAsync(request, Fingerprint);

        ClassroomConnectionResult result = await harness.Service.ConnectAsync(new ClassroomConnectionRequest(request));

        Assert.False(result.IsConnected);
        Assert.Equal(HostTrustState.Trusted, result.TrustState);
        Assert.Equal(1, harness.HostClient.HealthCalls);
        Assert.Equal(0, harness.HostClient.SessionCalls);
        Assert.Contains("profile", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionService_IsRegisteredInClassroomClientServices()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            IClassroomConnectionService service =
                provider.GetRequiredService<IClassroomConnectionService>();

            Assert.IsType<ClassroomConnectionService>(service);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static TestHarness CreateHarness(string? dataDirectory = null)
    {
        dataDirectory ??= CreateTempDirectory();
        var privateRepository = new StudentPrivateRepository(dataDirectory);
        var credentialStore = new InMemoryClassroomCredentialStore();
        var profileService = new FileClassroomProfileService(
            dataDirectory,
            privateRepository,
            credentialStore);
        var modeService = new InMemoryClassroomModeService();
        var connectionService = new InMemoryClassroomHostConnectionService();
        var hostClient = new RecordingHostClient();
        var trustService = new HostTrustService(new InMemoryHostTrustStore(), new ClassroomJoinParser());
        var service = new ClassroomConnectionService(
            trustService,
            profileService,
            hostClient,
            connectionService,
            modeService);

        return new TestHarness(
            service,
            trustService,
            profileService,
            modeService,
            connectionService,
            hostClient,
            dataDirectory);
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-connection-service-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private sealed record TestHarness(
        ClassroomConnectionService Service,
        IHostTrustService TrustService,
        IProfileService ProfileService,
        IClassroomModeService ModeService,
        IClassroomHostConnectionService ConnectionService,
        RecordingHostClient HostClient,
        string DataDirectory) : IDisposable
    {
        public void Dispose()
        {
            if (ProfileService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            CleanupTempDirectory(DataDirectory);
        }
    }

    private sealed class RecordingHostClient : ILibraryHostClient
    {
        public int HealthCalls { get; private set; }

        public int SessionCalls { get; private set; }

        public string HealthFingerprint { get; set; } = Fingerprint;

        public Guid ProfileId { get; private set; }

        public ClassroomRole Role { get; private set; }

        public TimeSpan Lifetime { get; private set; }

        public Task<LibraryHostHealth> GetHealthAsync(
            ClassroomJoinRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HealthCalls++;
            return Task.FromResult(new LibraryHostHealth(
                request.DisplayName ?? "School Library",
                HealthFingerprint,
                "file-stream"));
        }

        public Task<LibraryHostSession> IssueSessionAsync(
            ClassroomJoinRequest request,
            Guid profileId,
            ClassroomRole role,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            SessionCalls++;
            ProfileId = profileId;
            Role = role;
            Lifetime = lifetime;
            return Task.FromResult(new LibraryHostSession(
                "issued-session-token",
                DateTimeOffset.UtcNow.Add(lifetime)));
        }

        public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostCatalogueQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostBookDetail> GetBookAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSearchPage> SearchCatalogueAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostSearchQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetPageRenderAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            int pageNumber,
            int widthPx,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetFileStreamAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetAssetAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string assetUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
