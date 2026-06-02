namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Controls Standalone versus Client/Classroom runtime mode.</summary>
public interface IClassroomModeService
{
    /// <summary>Observable runtime Client-mode connectivity status changes.</summary>
    IObservable<ClassroomConnectivityStatus> Connectivity { get; }

    /// <summary>Gets the current mode without opening network connections.</summary>
    Task<ClassroomModeSettings> GetModeAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a mode change. Switching into Client mode requires explicit UI confirmation.</summary>
    Task SaveModeAsync(ClassroomModeSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Gets the latest runtime Host connectivity status.</summary>
    Task<ClassroomConnectivityStatus> GetConnectivityAsync(CancellationToken cancellationToken = default);

    /// <summary>Publishes a runtime Host connectivity status change.</summary>
    Task SetConnectivityAsync(
        ClassroomConnectivityStatus status,
        CancellationToken cancellationToken = default);
}
