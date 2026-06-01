namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Owns per-profile private classroom state outside the standalone catalogue DB.</summary>
public interface IStudentPrivateRepository
{
    /// <summary>Gets the private database path that belongs to a profile.</summary>
    string GetPrivateDatabasePath(Guid profileId);

    /// <summary>Ensures the profile's private database exists and is schema-current.</summary>
    Task EnsureCreatedAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the private database for a profile.</summary>
    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
