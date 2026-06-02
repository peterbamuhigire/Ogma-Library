using System.Security.Cryptography;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Manual opt-in sync orchestration for encrypted per-profile private state.</summary>
internal sealed class ClassroomSyncService : ISyncService
{
    private const string NoActiveProfileMessage = "No classroom profile is active.";
    private const string GuestSyncMessage = "Guest sessions do not sync private state.";
    private const string NoActiveConnectionMessage = "Connect to a classroom Host before syncing.";

    private readonly IProfileService _profiles;
    private readonly IClassroomHostConnectionService _connections;
    private readonly IStudentPrivateRepository _privateRepository;
    private readonly IClassroomSyncBlobCodec _codec;
    private readonly ILibraryHostClient _hostClient;

    public ClassroomSyncService(
        IProfileService profiles,
        IClassroomHostConnectionService connections,
        IStudentPrivateRepository privateRepository,
        IClassroomSyncBlobCodec codec,
        ILibraryHostClient hostClient)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _privateRepository = privateRepository ?? throw new ArgumentNullException(nameof(privateRepository));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
    }

    public async Task<ClassroomSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ClassroomProfile? profile = await _profiles.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Disabled(NoActiveProfileMessage);
        }

        if (profile.IsGuest || profile.Role == ClassroomRole.Guest)
        {
            return Disabled(GuestSyncMessage);
        }

        ClassroomHostConnection? connection = await _connections.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Disabled(NoActiveConnectionMessage);
        }

        string hostId = HostTrustService.CreateHostKey(connection.Request);
        StudentSyncState? state = await _privateRepository
            .GetSyncStateAsync(profile.ProfileId, hostId, cancellationToken)
            .ConfigureAwait(false);

        return new ClassroomSyncStatus(
            IsEnabled: true,
            IsRunning: false,
            LastSyncedUtc: state?.LastSyncedUtc,
            ConflictCount: state?.ConflictCount ?? 0,
            ErrorMessage: null);
    }

    public async Task<ClassroomSyncStatus> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        ClassroomProfile? profile = await _profiles.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Disabled(NoActiveProfileMessage);
        }

        if (profile.IsGuest || profile.Role == ClassroomRole.Guest)
        {
            return Disabled(GuestSyncMessage);
        }

        ClassroomHostConnection? connection = await _connections.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Disabled(NoActiveConnectionMessage);
        }

        string hostId = HostTrustService.CreateHostKey(connection.Request);
        StudentSyncState? previousState = await _privateRepository
            .GetSyncStateAsync(profile.ProfileId, hostId, cancellationToken)
            .ConfigureAwait(false);

        ClassroomSyncSnapshot snapshot = new(
            profile.ProfileId,
            hostId,
            DateTimeOffset.UtcNow,
            await _privateRepository.ListReadingProgressAsync(profile.ProfileId, hostId, cancellationToken)
                .ConfigureAwait(false),
            await _privateRepository.ListAnnotationsForHostAsync(profile.ProfileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false),
            await _privateRepository.ListBookmarksForHostAsync(profile.ProfileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false),
            await _privateRepository.ListAiHistoryAsync(profile.ProfileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false),
            previousState);

        EncryptedClassroomSyncBlob uploadBlob = _codec.Encode(snapshot, connection.SessionToken);
        await _hostClient
            .UploadProfileSyncBlobAsync(connection.Request, connection.SessionToken, uploadBlob, cancellationToken)
            .ConfigureAwait(false);

        EncryptedClassroomSyncBlob? downloadedBlob = await _hostClient
            .DownloadProfileSyncBlobAsync(connection.Request, connection.SessionToken, cancellationToken)
            .ConfigureAwait(false);
        byte[] hashSource = downloadedBlob?.Content ?? uploadBlob.Content;
        string blobHash = Convert.ToHexString(SHA256.HashData(hashSource)).ToLowerInvariant();
        DateTimeOffset syncedUtc = DateTimeOffset.UtcNow;
        var state = new StudentSyncState(hostId, syncedUtc, blobHash, ConflictCount: 0);
        await _privateRepository.SaveSyncStateAsync(profile.ProfileId, state, cancellationToken).ConfigureAwait(false);

        return new ClassroomSyncStatus(
            IsEnabled: true,
            IsRunning: false,
            LastSyncedUtc: syncedUtc,
            ConflictCount: 0,
            ErrorMessage: null);
    }

    private static ClassroomSyncStatus Disabled(string errorMessage) =>
        new(IsEnabled: false, IsRunning: false, LastSyncedUtc: null, ConflictCount: 0, ErrorMessage: errorMessage);
}
