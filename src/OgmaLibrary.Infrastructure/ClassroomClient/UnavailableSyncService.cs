using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Sync is opt-in and lands after private storage and Host sync endpoints.</summary>
internal sealed class UnavailableSyncService : ISyncService
{
    private static readonly ClassroomSyncStatus Disabled =
        new(IsEnabled: false, IsRunning: false, LastSyncedUtc: null, ConflictCount: 0, ErrorMessage: null);

    public Task<ClassroomSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Disabled);
    }

    public Task<ClassroomSyncStatus> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Disabled with { ErrorMessage = "Classroom sync is not enabled yet." });
    }
}
