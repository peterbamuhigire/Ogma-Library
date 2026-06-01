using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN Host load smoke tests.</summary>
public sealed class LanHostLoadSmokeTests
{
    [Fact]
    public async Task CatalogueEndpoint_HandlesTwentyConcurrentAuthenticatedClients()
    {
        string dataDirectory = CreateTempDirectory();
        int port = GetFreeTcpPort();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBooksAsync(services, count: 40);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Load Smoke"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            await host.StartAsync();

            using HttpClient http = CreatePinnedTestClient(port);
            using HttpResponseMessage session = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new { clientId = "load-smoke", role = "Teacher", lifetimeMinutes = 5 });
            string token = await ReadJsonStringAsync(session, "token");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Task<long>[] requests = Enumerable.Range(0, 20)
                .Select(_ => TimedGetAsync(http, "/api/v1/catalogue?page=1&pageSize=20"))
                .ToArray();
            long[] elapsedMs = await Task.WhenAll(requests);

            await host.StopAsync();

            Array.Sort(elapsedMs);
            long p95 = elapsedMs[(int)Math.Ceiling(elapsedMs.Length * 0.95) - 1];
            Assert.True(p95 < 2_000, $"Expected catalogue P95 < 2000 ms, actual {p95} ms.");
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<long> TimedGetAsync(HttpClient http, string path)
    {
        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await http.GetAsync(path);
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Load Smoke Book", json, StringComparison.Ordinal);
        return stopwatch.ElapsedMilliseconds;
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddLanHostServices(dataDirectory)
            .AddSingleton<ILanBindAddressSelector>(new StaticLanBindAddressSelector(IPAddress.Loopback))
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static async Task SeedBooksAsync(ServiceProvider services, int count)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        for (int i = 0; i < count; i++)
        {
            string suffix = i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            context.Books.Add(new BookRow
            {
                BookId = $"01LANLOADSMOKE000000{suffix}",
                Title = $"Load Smoke Book {suffix}",
                RelativePath = $"load-smoke-{suffix}.pdf",
                Sha256Hash = new string('a', 64),
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
                        RelativePath = $"load-smoke-{suffix}.pdf",
                        FileStatus = 0,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    },
                ],
            });
        }

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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-load-{Guid.NewGuid():N}");
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

    private sealed class StaticLanBindAddressSelector(IPAddress address) : ILanBindAddressSelector
    {
        public IPAddress SelectBindAddress() => address;
    }
}
