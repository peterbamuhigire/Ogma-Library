namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Creates, selects, lists, and deletes local classroom profiles.</summary>
public interface IProfileService
{
    /// <summary>Creates a persistent student or teacher profile.</summary>
    Task<ClassroomProfile> CreateAsync(
        CreateClassroomProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a transient guest profile that writes no private database rows.</summary>
    Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the active transient guest profile, when present.</summary>
    Task ClearGuestSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists persistent local profiles.</summary>
    Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Selects an active persistent profile.</summary>
    Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Gets the active persistent or guest profile, when one is selected.</summary>
    Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes a persistent profile and its private state.</summary>
    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Stores the Host session token for a persistent profile.</summary>
    Task StoreSessionTokenAsync(
        Guid profileId,
        string sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the Host session token for a persistent profile, when one exists.</summary>
    Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Clears the Host session token for a persistent profile.</summary>
    Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default);
}
