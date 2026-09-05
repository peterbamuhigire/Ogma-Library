using System.IO.Compression;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>In-memory offline cache scaffold; the LRU disk cache lands in WP7.</summary>
internal sealed class InMemoryOfflineCacheService : IOfflineCacheService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly Dictionary<(string HostId, string ResourceKey), OfflineCacheEntry> _entries = [];

    public Task<OfflineCacheEntry?> GetAsync(
        string hostId,
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue((hostId, resourceKey), out OfflineCacheEntry? entry);
        return Task.FromResult(entry);
    }

    public Task PutAsync(OfflineCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        _entries[(entry.HostId, entry.ResourceKey)] = entry;
        return Task.CompletedTask;
    }

    public Task ClearHostAsync(string hostId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        cancellationToken.ThrowIfCancellationRequested();

        foreach ((string entryHostId, string resourceKey) in _entries.Keys.ToArray())
        {
            if (entryHostId == hostId)
            {
                _entries.Remove((entryHostId, resourceKey));
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.Clear();
        return Task.CompletedTask;
    }

    public async Task ExportHostAsync(
        string hostId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var archive = new ZipArchive(
            destination,
            ZipArchiveMode.Create,
            leaveOpen: true);
        var manifest = new List<OfflineCacheExportItem>();
        int index = 0;
        foreach (OfflineCacheEntry entry in _entries.Values
            .Where(entry => string.Equals(entry.HostId, hostId, StringComparison.Ordinal))
            .OrderBy(entry => entry.ResourceKey, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = $"resources/{index++}.bin";
            ZipArchiveEntry archiveEntry = archive.CreateEntry(name);
            Stream resourceStream = archiveEntry.Open();
            await using (resourceStream.ConfigureAwait(false))
            {
                await resourceStream.WriteAsync(entry.Content, cancellationToken).ConfigureAwait(false);
            }

            manifest.Add(new OfflineCacheExportItem(
                entry.ResourceKey,
                entry.ETag,
                entry.StoredUtc,
                entry.ContentType,
                name,
                entry.Content.LongLength));
        }

        ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json");
        Stream manifestStream = manifestEntry.Open();
        await using (manifestStream.ConfigureAwait(false))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                manifestStream,
                new OfflineCacheExportManifest(1, hostId, manifest),
                JsonOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record OfflineCacheExportManifest(
        int SchemaVersion,
        string HostId,
        IReadOnlyList<OfflineCacheExportItem> Resources);

    private sealed record OfflineCacheExportItem(
        string ResourceKey,
        string? ETag,
        DateTimeOffset StoredUtc,
        string ContentType,
        string ArchivePath,
        long ContentLength);
}
