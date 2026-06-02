using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>In-memory profile service used until the per-student private DB lands.</summary>
internal sealed class InMemoryProfileService : IProfileService
{
    private readonly List<ClassroomProfile> _profiles = [];
    private ClassroomProfile? _active;

    public Task<ClassroomProfile> CreateAsync(
        CreateClassroomProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Profile display name is required.", nameof(request));
        }

        if (request.Role == ClassroomRole.Guest)
        {
            throw new ArgumentException("Use CreateGuestSessionAsync for guest profiles.", nameof(request));
        }

        var profile = new ClassroomProfile(
            Guid.NewGuid(),
            request.DisplayName.Trim(),
            request.Role,
            IsGuest: false);
        _profiles.Add(profile);
        _active = profile;
        return Task.FromResult(profile);
    }

    public Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _active = new ClassroomProfile(Guid.NewGuid(), "Guest", ClassroomRole.Guest, IsGuest: true);
        return Task.FromResult(_active);
    }

    public Task ClearGuestSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_active is { IsGuest: true })
        {
            _active = null;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ClassroomProfile>>(_profiles.ToArray());
    }

    public Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _active = _profiles.SingleOrDefault(profile => profile.ProfileId == profileId) ??
            throw new InvalidOperationException("Classroom profile was not found.");
        return Task.CompletedTask;
    }

    public Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_active);
    }

    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profiles.RemoveAll(profile => profile.ProfileId == profileId);
        if (_active?.ProfileId == profileId)
        {
            _active = null;
        }

        return Task.CompletedTask;
    }

    public Task StoreSessionTokenAsync(
        Guid profileId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Session tokens require a credential store.");
    }

    public Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Session tokens require a credential store.");
    }

    public Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Session tokens require a credential store.");
    }
}
