using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>In-memory offline cache scaffold; the LRU disk cache lands in WP7.</summary>
internal sealed class InMemoryOfflineCacheService : IOfflineCacheService
{
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
}
