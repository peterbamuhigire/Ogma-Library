using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN Host persistence tests.</summary>
public sealed class LanHostPersistenceTests
{
    [Fact]
    public async Task HostModeSettingsRepository_DefaultsPersistAndRoundTrip()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            var repository = services.GetRequiredService<IHostModeSettingsRepository>();

            HostModeSettings defaults = await repository.GetAsync(CancellationToken.None);
            await repository.SaveAsync(
                new HostModeSettings(true, 8484, HostContentDeliveryMode.FileStream, "Ogma School Library"),
                CancellationToken.None);

            HostModeSettings saved = await repository.GetAsync(CancellationToken.None);

            Assert.False(defaults.IsEnabled);
            Assert.Equal(7473, defaults.Port);
            Assert.Equal(HostContentDeliveryMode.PageRender, defaults.ContentMode);
            Assert.True(saved.IsEnabled);
            Assert.Equal(8484, saved.Port);
            Assert.Equal(HostContentDeliveryMode.FileStream, saved.ContentMode);
            Assert.Equal("Ogma School Library", saved.DisplayName);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClientSessionService_StoresOnlyTokenHashAndRevokesOnStop()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            var sessions = services.GetRequiredService<IClientSessionService>();

            ClientSessionResult issued = await sessions.IssueAsync(
                new ClientSessionRequest("student-tablet-1", "Student", TimeSpan.FromMinutes(20)),
                CancellationToken.None);

            bool validBeforeRevoke = await sessions.IsValidAsync(issued.Token, CancellationToken.None);
            await sessions.RevokeAllAsync(CancellationToken.None);
            bool validAfterRevoke = await sessions.IsValidAsync(issued.Token, CancellationToken.None);

            await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
            var row = await context.HostClientSessions.SingleAsync();

            Assert.True(validBeforeRevoke);
            Assert.False(validAfterRevoke);
            Assert.NotEqual(issued.Token, row.TokenHash);
            Assert.Equal(64, row.TokenHash.Length);
            Assert.NotNull(row.RevokedUtc);
            Assert.DoesNotContain(issued.Token, row.ClientId, StringComparison.Ordinal);
            Assert.DoesNotContain(issued.Token, row.Role, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClientSessionService_CountActive_IgnoresExpiredAndRevokedSessions()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            var sessions = services.GetRequiredService<IClientSessionService>();

            ClientSessionResult active = await sessions.IssueAsync(
                new ClientSessionRequest("student-tablet-1", "Student", TimeSpan.FromMinutes(20)),
                CancellationToken.None);
            ClientSessionResult revoked = await sessions.IssueAsync(
                new ClientSessionRequest("student-tablet-2", "Student", TimeSpan.FromMinutes(20)),
                CancellationToken.None);

            await using (CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>())
            {
                HostClientSessionRow revokedRow = await context.HostClientSessions
                    .SingleAsync(x => x.TokenHash == ClientSessionService.HashToken(revoked.Token));
                revokedRow.RevokedUtc = DateTimeOffset.UtcNow;
                context.HostClientSessions.Add(new HostClientSessionRow
                {
                    TokenHash = new string('a', 64),
                    ClientId = "expired-tablet",
                    Role = "Student",
                    IssuedUtc = DateTimeOffset.UtcNow.AddHours(-2),
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(-1),
                });
                await context.SaveChangesAsync();
            }

            int activeCount = await sessions.CountActiveAsync(CancellationToken.None);

            Assert.True(await sessions.IsValidAsync(active.Token, CancellationToken.None));
            Assert.Equal(1, activeCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Phase16Migration_AddsLanHostTablesAndDefaultSettings()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();

            bool settingsSeeded = await context.HostModeSettings
                .AnyAsync(x => x.SettingsId == "default" && x.Port == 7473);
            IReadOnlyList<string> tableNames = await context.Database
                .SqlQueryRaw<string>(
                    "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name IN ('HostModeSettings', 'HostClientSessions')")
                .ToListAsync();

            Assert.True(settingsSeeded);
            Assert.Contains("HostModeSettings", tableNames);
            Assert.Contains("HostClientSessions", tableNames);
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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
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
