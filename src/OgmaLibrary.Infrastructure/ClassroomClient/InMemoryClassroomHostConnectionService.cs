using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Runtime-only active Host connection store for Client mode.</summary>
internal sealed class InMemoryClassroomHostConnectionService : IClassroomHostConnectionService
{
    private ClassroomHostConnection? _active;

    public Task<ClassroomHostConnection?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_active);
    }

    public Task SetActiveAsync(
        ClassroomHostConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(connection.Request);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.SessionToken);
        cancellationToken.ThrowIfCancellationRequested();
        _active = connection;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _active = null;
        return Task.CompletedTask;
    }
}
