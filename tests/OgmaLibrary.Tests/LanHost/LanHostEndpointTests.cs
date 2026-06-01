using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 HTTPS listener endpoint tests.</summary>
public sealed class LanHostEndpointTests
{
    [Fact]
    public async Task HostListener_HealthAuthAndCatalogueProjection_WorkOverHttps()
    {
        string dataDirectory = CreateTempDirectory();
        int port = GetFreeTcpPort();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookAsync(services);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Endpoint Test"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            await host.StartAsync();

            using HttpClient http = CreatePinnedTestClient(port);
            using HttpResponseMessage health = await http.GetAsync("/api/v1/health");
            using HttpResponseMessage unauthorized = await http.GetAsync("/api/v1/catalogue?pageSize=10");
            using HttpResponseMessage session = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new { clientId = "student-1", role = "Student", lifetimeMinutes = 5 });
            string token = await ReadJsonStringAsync(session, "token");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage catalogue = await http.GetAsync("/api/v1/catalogue?pageSize=10");
            string catalogueJson = await catalogue.Content.ReadAsStringAsync();

            await host.StopAsync();
            await using CatalogueDbContext verify = services.GetRequiredService<CatalogueDbContext>();
            List<AuditEventRow> auditEvents = await verify.AuditEvents
                .Where(x => x.EventType == "LanHostRequestServed")
                .ToListAsync();

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            Assert.Equal(HttpStatusCode.OK, catalogue.StatusCode);
            Assert.Contains("LAN Endpoint Book", catalogueJson, StringComparison.Ordinal);
            Assert.Contains("01LANENDPOINT000000000001", catalogueJson, StringComparison.Ordinal);
            Assert.True(auditEvents.Count >= 4);
            Assert.Contains(auditEvents, e => e.EntityId == "/api/v1/catalogue" && e.AfterJson?.Contains("\"statusCode\":401", StringComparison.Ordinal) == true);
            Assert.Contains(auditEvents, e => e.EntityId == "/api/v1/catalogue" && e.ActorId?.StartsWith("session:", StringComparison.Ordinal) == true);
            Assert.DoesNotContain(auditEvents, e => e.AfterJson?.Contains(token, StringComparison.Ordinal) == true);
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
            .AddLanHostServices(dataDirectory)
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static async Task SeedBookAsync(ServiceProvider services)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        context.Books.Add(new BookRow
        {
            BookId = "01LANENDPOINT000000000001",
            Title = "LAN Endpoint Book",
            RelativePath = "lan-endpoint-book.pdf",
            Sha256Hash = new string('d', 64),
            SizeBytes = 128,
            MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
            Status = 0,
            IndexStatus = 0,
            EmbeddingStatus = 0,
            IsOcrDerived = false,
            IsPasswordProtected = false,
            Year = 2026,
            BookFiles =
            [
                new BookFileRow
                {
                    RelativePath = "lan-endpoint-book.pdf",
                    FileStatus = 0,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                },
            ],
        });
        await context.SaveChangesAsync();
    }

    private static HttpClient CreatePinnedTestClient(int port)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://127.0.0.1:{port}"),
        };
    }

    private static async Task<string> ReadJsonStringAsync(HttpResponseMessage response, string propertyName)
    {
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-endpoint-{Guid.NewGuid():N}");
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
