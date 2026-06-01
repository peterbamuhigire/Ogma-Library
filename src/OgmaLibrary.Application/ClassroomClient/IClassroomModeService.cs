namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Controls Standalone versus Client/Classroom runtime mode.</summary>
public interface IClassroomModeService
{
    /// <summary>Gets the current mode without opening network connections.</summary>
    Task<ClassroomModeSettings> GetModeAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a mode change. Switching into Client mode requires explicit UI confirmation.</summary>
    Task SaveModeAsync(ClassroomModeSettings settings, CancellationToken cancellationToken = default);
}
