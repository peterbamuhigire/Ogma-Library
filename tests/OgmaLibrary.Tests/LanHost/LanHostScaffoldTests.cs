using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN Host scaffold tests.</summary>
public sealed class LanHostScaffoldTests
{
    [Fact]
    public async Task HostModeSettings_Defaults_AreStandaloneSafe()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);

            HostModeSettings settings = await services.GetRequiredService<IHostModeSettingsRepository>()
                .GetAsync(CancellationToken.None);
            LibraryHostStatus status = await services.GetRequiredService<ILibraryHostService>()
                .GetStatusAsync(CancellationToken.None);

            Assert.False(settings.IsEnabled);
            Assert.Equal(7473, settings.Port);
            Assert.Equal(HostContentDeliveryMode.PageRender, settings.ContentMode);
            Assert.Equal(LibraryHostState.Stopped, status.State);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task LibraryHostService_StartStop_AdvertisesFingerprintAndRevokesSessions()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            var sessions = services.GetRequiredService<IClientSessionService>();
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(false, GetFreeTcpPort(), HostContentDeliveryMode.PageRender, "Ogma Library"));
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
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddLanHostServices()
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
