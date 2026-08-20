using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Tests.Reader;

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
            await SeedBooksAsync(services, dataDirectory, count: 40);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Load Smoke"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            LibraryHostStatus started = await host.StartAsync();
            string enrollmentCode = started.EnrollmentCode ?? throw new InvalidOperationException("Host did not issue an enrollment code.");

            using HttpClient http = CreatePinnedTestClient(port);
            using HttpResponseMessage session = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new { clientId = "load-smoke", role = "Teacher", lifetimeMinutes = 5, enrollmentCode });
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

    [Fact]
    public async Task PageRenderEndpoint_HandlesTenConcurrentAuthenticatedClients()
    {
        string dataDirectory = CreateTempDirectory();
        int port = GetFreeTcpPort();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBooksAsync(services, dataDirectory, count: 1, createFiles: true);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Page Load Smoke"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            LibraryHostStatus started = await host.StartAsync();
            string enrollmentCode = started.EnrollmentCode ?? throw new InvalidOperationException("Host did not issue an enrollment code.");

            using HttpClient http = CreatePinnedTestClient(port);
            using HttpResponseMessage session = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new { clientId = "page-load-smoke", role = "Teacher", lifetimeMinutes = 5, enrollmentCode });
            string token = await ReadJsonStringAsync(session, "token");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Task<long>[] requests = Enumerable.Range(0, 10)
                .Select(_ => TimedPngGetAsync(http, "/api/v1/books/01LANLOADSMOKE0000000000/page/1?widthPx=800"))
                .ToArray();
            long[] elapsedMs = await Task.WhenAll(requests);

            await host.StopAsync();

            Array.Sort(elapsedMs);
            long p95 = elapsedMs[(int)Math.Ceiling(elapsedMs.Length * 0.95) - 1];
            Assert.True(p95 < 2_000, $"Expected page-render P95 < 2000 ms, actual {p95} ms.");
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
        string json = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected HTTP 200 from {path}; received {(int)response.StatusCode} {response.StatusCode}. Body: {json}");
        Assert.Contains("Load Smoke Book", json, StringComparison.Ordinal);
        return stopwatch.ElapsedMilliseconds;
    }

    private static async Task<long> TimedPngGetAsync(HttpClient http, string path)
    {
        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await http.GetAsync(path);
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        byte[] png = await response.Content.ReadAsByteArrayAsync();
        Assert.True(png.Length >= 8);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
        return stopwatch.ElapsedMilliseconds;
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddSingleton<IPdfRendererFactory>(new MockPdfRendererFactory(pageCount: 3))
            .AddLanHostServices(dataDirectory)
            .AddSingleton<ILanBindAddressSelector>(new StaticLanBindAddressSelector(IPAddress.Loopback))
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static async Task SeedBooksAsync(
        ServiceProvider services,
        string dataDirectory,
        int count,
        bool createFiles = false)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        for (int i = 0; i < count; i++)
        {
            string suffix = i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            string relativePath = $"load-smoke-{suffix}.pdf";
            context.Books.Add(new BookRow
            {
                BookId = $"01LANLOADSMOKE000000{suffix}",
                Title = $"Load Smoke Book {suffix}",
                RelativePath = relativePath,
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
                        RelativePath = relativePath,
                        FileStatus = 0,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    },
                ],
            });

            if (createFiles)
            {
                string path = Path.Combine(dataDirectory, relativePath);
                await File.WriteAllBytesAsync(path, "%PDF-1.7\n% Ogma page load fixture\n"u8.ToArray());
            }
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
