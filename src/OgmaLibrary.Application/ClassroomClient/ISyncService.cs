namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Coordinates optional private-state sync for the active classroom profile.</summary>
public interface ISyncService
{
    /// <summary>Gets current sync state without uploading private data.</summary>
    Task<ClassroomSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs an explicit user-triggered sync for the active profile.</summary>
    Task<ClassroomSyncStatus> SyncNowAsync(CancellationToken cancellationToken = default);
}
