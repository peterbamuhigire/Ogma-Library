using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>File-backed classroom profile service for Phase 17 onboarding.</summary>
internal sealed class FileClassroomProfileService : IProfileService, IDisposable
{
    internal const string SessionTokenKeyPrefix = "ogma.classroom.session.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _storePath;
    private readonly IStudentPrivateRepository _privateRepository;
    private readonly IClassroomCredentialStore _credentialStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClassroomProfile? _guest;

    public FileClassroomProfileService(
        string dataDirectory,
        IStudentPrivateRepository privateRepository,
        IClassroomCredentialStore credentialStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _privateRepository = privateRepository ?? throw new ArgumentNullException(nameof(privateRepository));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _storePath = Path.Combine(dataDirectory, "classroom", "profiles", "profiles.json");
    }

    public async Task<ClassroomProfile> CreateAsync(
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

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var profile = new ClassroomProfile(
                Guid.NewGuid(),
                request.DisplayName.Trim(),
                request.Role,
                IsGuest: false);

            snapshot.Profiles.Add(profile);
            snapshot.ActiveProfileId = profile.ProfileId;
            await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await _privateRepository.EnsureCreatedAsync(profile.ProfileId, cancellationToken).ConfigureAwait(false);
            _guest = null;
            return profile;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _guest = new ClassroomProfile(Guid.NewGuid(), "Guest", ClassroomRole.Guest, IsGuest: true);
        return Task.FromResult(_guest);
    }

    public Task ClearGuestSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_guest is { IsGuest: true })
        {
            _guest = null;
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.Profiles.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        ThrowIfEmptyProfileId(profileId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!snapshot.Profiles.Any(profile => profile.ProfileId == profileId))
            {
                throw new InvalidOperationException("Classroom profile was not found.");
            }

            snapshot.ActiveProfileId = profileId;
            await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _guest = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_guest is not null)
        {
            return _guest;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.ActiveProfileId is null
                ? null
                : snapshot.Profiles.SingleOrDefault(profile => profile.ProfileId == snapshot.ActiveProfileId.Value);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        ThrowIfEmptyProfileId(profileId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            int removed = snapshot.Profiles.RemoveAll(profile => profile.ProfileId == profileId);
            if (removed == 0)
            {
                return;
            }

            if (snapshot.ActiveProfileId == profileId)
            {
                snapshot.ActiveProfileId = null;
            }

            await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await _privateRepository.DeleteAsync(profileId, cancellationToken).ConfigureAwait(false);
            await _credentialStore.DeleteSecretAsync(CreateSessionTokenKey(profileId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StoreSessionTokenAsync(
        Guid profileId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        ThrowIfEmptyProfileId(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        await EnsurePersistentProfileExistsAsync(profileId, cancellationToken).ConfigureAwait(false);
        await _credentialStore.SaveSecretAsync(
                CreateSessionTokenKey(profileId),
                sessionToken,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        ThrowIfEmptyProfileId(profileId);
        return _credentialStore.GetSecretAsync(CreateSessionTokenKey(profileId), cancellationToken);
    }

    public Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        ThrowIfEmptyProfileId(profileId);
        return _credentialStore.DeleteSecretAsync(CreateSessionTokenKey(profileId), cancellationToken);
    }

    internal static string CreateSessionTokenKey(Guid profileId) =>
        $"{SessionTokenKeyPrefix}{profileId:N}";

    private async Task EnsurePersistentProfileExistsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProfileStoreSnapshot snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!snapshot.Profiles.Any(profile => profile.ProfileId == profileId))
            {
                throw new InvalidOperationException("Classroom profile was not found.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProfileStoreSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return new ProfileStoreSnapshot();
        }

        using FileStream stream = File.OpenRead(_storePath);
        ProfileStoreSnapshot? snapshot = await JsonSerializer
            .DeserializeAsync<ProfileStoreSnapshot>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return snapshot ?? new ProfileStoreSnapshot();
    }

    private async Task SaveAsync(ProfileStoreSnapshot snapshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        string tempPath = $"{_storePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        finally
        {
            DeleteTemporaryFile(tempPath);
        }
    }

    public void Dispose() => _gate.Dispose();

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ThrowIfEmptyProfileId(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }
    }

    private sealed class ProfileStoreSnapshot
    {
        public Guid? ActiveProfileId { get; set; }

        public List<ClassroomProfile> Profiles { get; set; } = [];
    }
}
