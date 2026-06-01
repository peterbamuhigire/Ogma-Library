using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Placeholder Host API client until discovery and TOFU are implemented.</summary>
internal sealed class UnavailableLibraryHostClient : ILibraryHostClient
{
    public Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }
}
