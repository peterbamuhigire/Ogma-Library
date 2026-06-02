namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Completes Client-mode onboarding against a trusted Library Host.</summary>
public interface IClassroomConnectionService
{
    /// <summary>
    /// Evaluates Host trust, issues a Host session, persists local connection state,
    /// and switches the installation into Client mode when successful.
    /// </summary>
    Task<ClassroomConnectionResult> ConnectAsync(
        ClassroomConnectionRequest request,
        CancellationToken cancellationToken = default);
}
