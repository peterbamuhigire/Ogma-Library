using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Phase 17 scaffold mode service; durable settings land with the client DB work package.</summary>
internal sealed class InMemoryClassroomModeService : IClassroomModeService
{
    private ClassroomModeSettings _settings = new(LibraryRuntimeMode.Standalone);

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
}
