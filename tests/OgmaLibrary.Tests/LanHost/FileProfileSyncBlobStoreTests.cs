using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Host profile-sync integrity and size-boundary tests.</summary>
public sealed class FileProfileSyncBlobStoreTests
{
    [Fact]
    public async Task ProfileSyncStore_RejectsTamperedPayload()
    {
        string root = CreateTempDirectory();

        try
        {
            var store = new FileProfileSyncBlobStore(root);
            await store.SaveAsync("profile-1", new HostProfileSyncBlob(
                "application/octet-stream",
                [1, 2, 3],
                DateTimeOffset.UtcNow));

            string payload = Directory.EnumerateFiles(
                    Path.Combine(root, "LanHost", "profile-sync"),
                    "*.blob",
                    SearchOption.TopDirectoryOnly)
                .Single();
            await File.WriteAllBytesAsync(payload, [9, 9, 9]);

            Assert.Null(await store.LoadAsync("profile-1"));
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ProfileSyncStore_RejectsOversizedPayload()
    {
        string root = CreateTempDirectory();

        try
        {
            var store = new FileProfileSyncBlobStore(root);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(
                "profile-1",
                new HostProfileSyncBlob(
                    "application/octet-stream",
                    new byte[(5 * 1024 * 1024) + 1],
                    DateTimeOffset.UtcNow)));
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-profile-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
