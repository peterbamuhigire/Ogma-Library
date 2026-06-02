namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Stores the current runtime Host connection for Client mode.</summary>
public interface IClassroomHostConnectionService
{
    /// <summary>Gets the active Host connection, or <see langword="null" /> when disconnected.</summary>
    Task<ClassroomHostConnection?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the active Host connection after onboarding and token issuance.</summary>
    Task SetActiveAsync(
        ClassroomHostConnection connection,
        CancellationToken cancellationToken = default);

    /// <summary>Clears the active Host connection.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
