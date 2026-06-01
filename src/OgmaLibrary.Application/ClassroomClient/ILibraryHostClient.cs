namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Typed client for the Phase 16 Library Host HTTP API.</summary>
public interface ILibraryHostClient
{
    /// <summary>Reads Host health metadata before enrolment.</summary>
    Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default);
}
