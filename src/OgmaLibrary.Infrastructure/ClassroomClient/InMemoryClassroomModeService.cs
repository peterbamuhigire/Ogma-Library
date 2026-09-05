using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Deterministic in-memory classroom mode service used by tests.</summary>
internal sealed class InMemoryClassroomModeService : IClassroomModeService
{
    private ClassroomModeSettings _settings = new(LibraryRuntimeMode.Standalone);
    private ClassroomSyncSettings _syncSettings = new();
    private readonly ObservableEvents<ClassroomConnectivityStatus> _connectivity = new();
    private ClassroomConnectivityStatus _connectivityStatus = new(
        IsOnline: false,
        UpdatedUtc: DateTimeOffset.MinValue,
        Message: "Not connected");

    public IObservable<ClassroomConnectivityStatus> Connectivity => _connectivity;

    public Task<ClassroomModeSettings> GetModeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_settings);
    }

    public Task SaveModeAsync(ClassroomModeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<ClassroomSyncSettings> GetSyncSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_syncSettings);
    }

    public Task SaveSyncSettingsAsync(
        ClassroomSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        _syncSettings = settings.IsEnabled ? settings : settings with { SyncOnReconnect = false };
        return Task.CompletedTask;
    }

    public Task<ClassroomConnectivityStatus> GetConnectivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_connectivityStatus);
    }

    public Task SetConnectivityAsync(
        ClassroomConnectivityStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        cancellationToken.ThrowIfCancellationRequested();
        _connectivityStatus = status;
        _connectivity.Publish(status);
        return Task.CompletedTask;
    }
}
