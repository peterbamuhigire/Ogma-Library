using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 offline cache tests.</summary>
public sealed class OfflineCacheServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DiskOfflineCache_PersistsAcrossServiceRestart()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using (var firstCache = new DiskOfflineCacheService(dataDirectory))
            {
                await firstCache.PutAsync(new OfflineCacheEntry(
                    "host-1",
                    "catalogue?page=1",
                    "etag-1",
                    [1, 2, 3],
                    Now));
            }

            using var secondCache = new DiskOfflineCacheService(dataDirectory);
            OfflineCacheEntry? cached = await secondCache.GetAsync("host-1", "catalogue?page=1");

            Assert.NotNull(cached);
            Assert.Equal("etag-1", cached.ETag);
            Assert.Equal([1, 2, 3], cached.Content);
            Assert.Equal(Now, cached.StoredUtc);
            Assert.Equal("application/octet-stream", cached.ContentType);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_IsScopedByHostAndCanClearOneHost()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory);
            await cache.PutAsync(new OfflineCacheEntry("host-a", "books/1/page/1", "a", [1], Now));
            await cache.PutAsync(new OfflineCacheEntry("host-b", "books/1/page/1", "b", [2], Now));

            await cache.ClearHostAsync("host-a");

            Assert.Null(await cache.GetAsync("host-a", "books/1/page/1"));
            Assert.Equal([2], (await cache.GetAsync("host-b", "books/1/page/1"))!.Content);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_EvictsLeastRecentlyUsed_WhenOverLimit()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory, sizeLimitBytes: 6);
            await cache.PutAsync(new OfflineCacheEntry("host-1", "a", null, [1, 1, 1], Now));
            await Task.Delay(20);
            await cache.PutAsync(new OfflineCacheEntry("host-1", "b", null, [2, 2, 2], Now.AddSeconds(1)));
            await Task.Delay(20);
            Assert.NotNull(await cache.GetAsync("host-1", "a"));
            await Task.Delay(20);

            await cache.PutAsync(new OfflineCacheEntry("host-1", "c", null, [3, 3, 3], Now.AddSeconds(2)));

            Assert.NotNull(await cache.GetAsync("host-1", "a"));
            Assert.Null(await cache.GetAsync("host-1", "b"));
            Assert.NotNull(await cache.GetAsync("host-1", "c"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void OfflineCacheService_IsDiskBackedInClassroomClientServices()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            IOfflineCacheService service = provider.GetRequiredService<IOfflineCacheService>();

            Assert.IsType<DiskOfflineCacheService>(service);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_PreservesContentType()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory);
            await cache.PutAsync(new OfflineCacheEntry(
                "host-1",
                "books/1/page/1",
                "etag-1",
                [1, 2, 3],
                Now,
                "image/png"));

            OfflineCacheEntry? cached = await cache.GetAsync("host-1", "books/1/page/1");

            Assert.Equal("image/png", cached!.ContentType);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_RejectsTamperedContentAndRemovesEntry()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory);
            await cache.PutAsync(new OfflineCacheEntry("host-1", "catalogue", "etag", [1, 2, 3], Now));

            string contentPath = Directory.EnumerateFiles(
                    Path.Combine(dataDirectory, "classroom", "cache"),
                    "*.bin",
                    SearchOption.TopDirectoryOnly)
                .Single();
            await File.WriteAllBytesAsync(contentPath, [9, 9, 9]);

            Assert.Null(await cache.GetAsync("host-1", "catalogue"));
            Assert.False(File.Exists(contentPath));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_RejectsTamperedMetadataLengthAndRemovesEntry()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory);
            await cache.PutAsync(new OfflineCacheEntry("host-1", "catalogue", "etag", [1, 2, 3], Now));

            string metadataPath = Directory.EnumerateFiles(
                    Path.Combine(dataDirectory, "classroom", "cache"),
                    "*.json",
                    SearchOption.TopDirectoryOnly)
                .Single();
            string metadata = await File.ReadAllTextAsync(metadataPath);
            metadata = metadata.Replace("\"contentLength\": 3", "\"contentLength\": 999", StringComparison.Ordinal);
            await File.WriteAllTextAsync(metadataPath, metadata);

            Assert.Null(await cache.GetAsync("host-1", "catalogue"));
            Assert.False(File.Exists(metadataPath));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DiskOfflineCache_RejectsMetadataPathOutsideCacheWithoutDeletingIt()
    {
        string dataDirectory = CreateTempDirectory();
        string externalPath = Path.Combine(dataDirectory, "must-survive.bin");

        try
        {
            using var cache = new DiskOfflineCacheService(dataDirectory);
            await cache.PutAsync(new OfflineCacheEntry("host-1", "catalogue", "etag", [1, 2, 3], Now));

            string cacheRoot = Path.Combine(dataDirectory, "classroom", "cache");
            string metadataPath = Directory.EnumerateFiles(cacheRoot, "*.json", SearchOption.TopDirectoryOnly).Single();
            await File.WriteAllBytesAsync(externalPath, [7, 7, 7]);
            string metadata = await File.ReadAllTextAsync(metadataPath);
            using JsonDocument document = JsonDocument.Parse(metadata);
            using var output = new MemoryStream();
            await using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("contentFile"))
                    {
                        writer.WriteString("contentFile", externalPath);
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            await File.WriteAllBytesAsync(metadataPath, output.ToArray());

            Assert.Null(await cache.GetAsync("host-1", "catalogue"));
            Assert.True(File.Exists(externalPath));
            Assert.False(File.Exists(metadataPath));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-offline-cache-{Guid.NewGuid():N}");
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
}
