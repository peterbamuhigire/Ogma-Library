using Microsoft.Extensions.DependencyInjection;
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
