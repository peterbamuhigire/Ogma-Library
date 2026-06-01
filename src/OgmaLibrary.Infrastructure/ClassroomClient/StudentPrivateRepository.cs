using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Creates the Phase 17 per-profile private database location.</summary>
internal sealed class StudentPrivateRepository : IStudentPrivateRepository
{
    private readonly string _profileRoot;

    public StudentPrivateRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _profileRoot = Path.Combine(dataDirectory, "classroom", "profiles");
    }

    public string GetPrivateDatabasePath(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        return Path.Combine(_profileRoot, profileId.ToString("N"), "private.db");
    }

    public Task EnsureCreatedAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetPrivateDatabasePath(profileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string profileDirectory = Path.GetDirectoryName(GetPrivateDatabasePath(profileId))!;
        if (Directory.Exists(profileDirectory))
        {
            Directory.Delete(profileDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }
}
