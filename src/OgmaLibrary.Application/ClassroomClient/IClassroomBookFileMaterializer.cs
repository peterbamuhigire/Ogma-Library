namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Materializes Host-served PDF bytes into a local file path for the reader.</summary>
public interface IClassroomBookFileMaterializer
{
    /// <summary>
    /// Downloads or reuses a Host file-stream PDF and returns a local path that can
    /// be opened by the existing PDF renderer.
    /// </summary>
    Task<string> MaterializeAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default);
}
