using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN Host scaffold tests.</summary>
public sealed class LanHostScaffoldTests
{
    [Fact]
    public async Task HostModeSettings_Defaults_AreStandaloneSafe()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLanHostServices()
            .BuildServiceProvider();

        HostModeSettings settings = await services.GetRequiredService<IHostModeSettingsRepository>()
            .GetAsync(CancellationToken.None);
        LibraryHostStatus status = await services.GetRequiredService<ILibraryHostService>()
            .GetStatusAsync(CancellationToken.None);

        Assert.False(settings.IsEnabled);
        Assert.Equal(7473, settings.Port);
        Assert.Equal(HostContentDeliveryMode.PageRender, settings.ContentMode);
        Assert.Equal(LibraryHostState.Stopped, status.State);
    }

    [Fact]
    public async Task LibraryHostService_StartStop_AdvertisesFingerprintAndRevokesSessions()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLanHostServices()
            .BuildServiceProvider();
        var sessions = services.GetRequiredService<IClientSessionService>();
        ClientSessionResult session = await sessions.IssueAsync(
            new ClientSessionRequest("client-1", "Student", TimeSpan.FromMinutes(30)),
            CancellationToken.None);

        LibraryHostStatus started = await services.GetRequiredService<ILibraryHostService>()
            .StartAsync(CancellationToken.None);
        bool sessionValidBeforeStop = await sessions.IsValidAsync(session.Token, CancellationToken.None);
        LibraryHostStatus stopped = await services.GetRequiredService<ILibraryHostService>()
            .StopAsync(CancellationToken.None);
        bool sessionValidAfterStop = await sessions.IsValidAsync(session.Token, CancellationToken.None);

        Assert.Equal(LibraryHostState.Running, started.State);
        Assert.Equal(64, started.CertificateFingerprint?.Length);
        Assert.True(sessionValidBeforeStop);
        Assert.Equal(LibraryHostState.Stopped, stopped.State);
        Assert.False(sessionValidAfterStop);
    }
}
