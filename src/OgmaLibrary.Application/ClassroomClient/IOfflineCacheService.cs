namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Stores Host-served catalogue/page resources for offline classroom reads.</summary>
public interface IOfflineCacheService
{
    /// <summary>Gets a cached resource, or <see langword="null" /> on cache miss.</summary>
    Task<OfflineCacheEntry?> GetAsync(
        string hostId,
        string resourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>Stores or replaces a cached resource.</summary>
    Task PutAsync(OfflineCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes all cached resources for a Host.</summary>
    Task ClearHostAsync(string hostId, CancellationToken cancellationToken = default);
}
