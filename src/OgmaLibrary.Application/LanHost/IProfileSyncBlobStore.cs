namespace OgmaLibrary.Application.LanHost;

/// <summary>Stores opaque private-state sync blobs by enrolled client profile.</summary>
public interface IProfileSyncBlobStore
{
    /// <summary>Saves or replaces the opaque sync blob for one client profile.</summary>
    Task SaveAsync(
        string clientId,
        HostProfileSyncBlob blob,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the latest opaque sync blob for one client profile.</summary>
    Task<HostProfileSyncBlob?> LoadAsync(
        string clientId,
        CancellationToken cancellationToken = default);
}
