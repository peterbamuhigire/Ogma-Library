namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Typed client for the Phase 16 Library Host HTTP API.</summary>
public interface ILibraryHostClient
{
    /// <summary>Reads Host health metadata before enrolment.</summary>
    Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a Host session token after enrollment-code confirmation.</summary>
    Task<LibraryHostSession> IssueSessionAsync(
        ClassroomJoinRequest request,
        Guid profileId,
        ClassroomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a page of Host catalogue summaries using an issued session token.</summary>
    Task<LibraryHostCataloguePage> GetCataloguePageAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostCatalogueQuery query,
        CancellationToken cancellationToken = default);
}
