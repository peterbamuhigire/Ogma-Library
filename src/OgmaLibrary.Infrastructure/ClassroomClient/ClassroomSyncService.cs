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

    public async Task<IReadOnlyList<StudentAnnotationConflict>> ListAnnotationConflictsAsync(
        CancellationToken cancellationToken = default)
    {
        ClassroomProfile? profile = await _profiles.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null || profile.IsGuest || profile.Role == ClassroomRole.Guest)
        {
            return [];
        }

        ClassroomHostConnection? connection = await _connections.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return [];
        }

        string hostId = HostTrustService.CreateHostKey(connection.Request);
        return await _privateRepository
            .ListAnnotationConflictsAsync(profile.ProfileId, hostId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ClassroomSyncStatus> ResolveAnnotationConflictAsync(
        string annotationId,
        ClassroomSyncConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
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
        IReadOnlyList<StudentAnnotationConflict> conflicts = await _privateRepository
            .ListAnnotationConflictsAsync(profile.ProfileId, hostId, cancellationToken)
            .ConfigureAwait(false);
        StudentAnnotationConflict? conflict = conflicts.SingleOrDefault(candidate => string.Equals(
            candidate.LocalAnnotation.Id,
            annotationId,
            StringComparison.Ordinal));
        if (conflict is null)
        {
            return await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        if (resolution == ClassroomSyncConflictResolution.KeepServer)
        {
            await _privateRepository
                .SaveAnnotationAsync(profile.ProfileId, conflict.RemoteAnnotation, cancellationToken)
                .ConfigureAwait(false);
        }

        await _privateRepository
            .DeleteAnnotationConflictAsync(profile.ProfileId, hostId, annotationId, cancellationToken)
            .ConfigureAwait(false);
        int remainingConflicts = conflicts.Count - 1;
        await SaveConflictCountAsync(profile.ProfileId, hostId, remainingConflicts, cancellationToken)
            .ConfigureAwait(false);

        StudentSyncState? state = await _privateRepository
            .GetSyncStateAsync(profile.ProfileId, hostId, cancellationToken)
            .ConfigureAwait(false);
        return new ClassroomSyncStatus(
            IsEnabled: true,
            IsRunning: false,
            LastSyncedUtc: state?.LastSyncedUtc,
            ConflictCount: remainingConflicts,
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
        int conflictCount = await DownloadAndMergeAsync(
                profile.ProfileId,
                hostId,
                connection,
                cancellationToken)
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
        var state = new StudentSyncState(hostId, syncedUtc, blobHash, conflictCount);
        await _privateRepository.SaveSyncStateAsync(profile.ProfileId, state, cancellationToken).ConfigureAwait(false);

        return new ClassroomSyncStatus(
            IsEnabled: true,
            IsRunning: false,
            LastSyncedUtc: syncedUtc,
            ConflictCount: conflictCount,
            ErrorMessage: null);
    }

    private static ClassroomSyncStatus Disabled(string errorMessage) =>
        new(IsEnabled: false, IsRunning: false, LastSyncedUtc: null, ConflictCount: 0, ErrorMessage: errorMessage);

    private async Task SaveConflictCountAsync(
        Guid profileId,
        string hostId,
        int conflictCount,
        CancellationToken cancellationToken)
    {
        StudentSyncState? previous = await _privateRepository
            .GetSyncStateAsync(profileId, hostId, cancellationToken)
            .ConfigureAwait(false);
        var next = new StudentSyncState(
            hostId,
            previous?.LastSyncedUtc,
            previous?.LastSyncBlobHash,
            Math.Max(0, conflictCount));
        await _privateRepository.SaveSyncStateAsync(profileId, next, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> DownloadAndMergeAsync(
        Guid profileId,
        string hostId,
        ClassroomHostConnection connection,
        CancellationToken cancellationToken)
    {
        EncryptedClassroomSyncBlob? remoteBlob = await _hostClient
            .DownloadProfileSyncBlobAsync(connection.Request, connection.SessionToken, cancellationToken)
            .ConfigureAwait(false);
        if (remoteBlob is null)
        {
            return 0;
        }

        ClassroomSyncSnapshot remoteSnapshot = _codec.Decode(remoteBlob, connection.SessionToken);
        if (remoteSnapshot.ProfileId != profileId || !string.Equals(remoteSnapshot.HostId, hostId, StringComparison.Ordinal))
        {
            return 0;
        }

        int conflictCount = 0;
        conflictCount += await MergeReadingProgressAsync(profileId, hostId, remoteSnapshot.ReadingProgress, cancellationToken)
            .ConfigureAwait(false);
        _ = await MergeAnnotationsAsync(profileId, hostId, remoteSnapshot.Annotations, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<StudentAnnotationConflict> annotationConflicts = await _privateRepository
            .ListAnnotationConflictsAsync(profileId, hostId, cancellationToken)
            .ConfigureAwait(false);
        conflictCount += annotationConflicts.Count;
        conflictCount += await MergeBookmarksAsync(profileId, hostId, remoteSnapshot.Bookmarks, cancellationToken)
            .ConfigureAwait(false);
        conflictCount += await MergeAiHistoryAsync(profileId, hostId, remoteSnapshot.AiHistory, cancellationToken)
            .ConfigureAwait(false);
        return conflictCount;
    }

    private async Task<int> MergeReadingProgressAsync(
        Guid profileId,
        string hostId,
        IReadOnlyList<StudentReadingProgress> remoteRows,
        CancellationToken cancellationToken)
    {
        Dictionary<string, StudentReadingProgress> localRows = (await _privateRepository
                .ListReadingProgressAsync(profileId, hostId, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(row => row.BookId, StringComparer.Ordinal);
        int conflicts = 0;
        foreach (StudentReadingProgress remote in remoteRows.Where(row => string.Equals(row.HostId, hostId, StringComparison.Ordinal)))
        {
            if (!localRows.TryGetValue(remote.BookId, out StudentReadingProgress? local))
            {
                await _privateRepository.SaveReadingProgressAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
                continue;
            }

            MergeDecision decision = Decide(
                local.UpdatedUtc,
                remote.UpdatedUtc,
                local.LastPage == remote.LastPage && Math.Abs(local.LastOffsetY - remote.LastOffsetY) < 0.001);
            if (decision == MergeDecision.UseRemote)
            {
                await _privateRepository.SaveReadingProgressAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
            }
            else if (decision == MergeDecision.Conflict)
            {
                conflicts++;
            }
        }

        return conflicts;
    }

    private async Task<int> MergeAnnotationsAsync(
        Guid profileId,
        string hostId,
        IReadOnlyList<StudentAnnotation> remoteRows,
        CancellationToken cancellationToken)
    {
        Dictionary<string, StudentAnnotation> localRows = (await _privateRepository
                .ListAnnotationsForHostAsync(profileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(row => row.Id, StringComparer.Ordinal);
        int conflicts = 0;
        foreach (StudentAnnotation remote in remoteRows.Where(row => string.Equals(row.HostId, hostId, StringComparison.Ordinal)))
        {
            if (!localRows.TryGetValue(remote.Id, out StudentAnnotation? local))
            {
                await _privateRepository.SaveAnnotationAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
                continue;
            }

            MergeDecision decision = Decide(local.UpdatedUtc, remote.UpdatedUtc, SameAnnotation(local, remote));
            if (decision == MergeDecision.UseRemote)
            {
                await _privateRepository.SaveAnnotationAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
            }
            else if (decision == MergeDecision.Conflict)
            {
                var conflict = new StudentAnnotationConflict(
                    hostId,
                    local,
                    remote,
                    DateTimeOffset.UtcNow);
                await _privateRepository
                    .SaveAnnotationConflictAsync(profileId, conflict, cancellationToken)
                    .ConfigureAwait(false);
                conflicts++;
            }
        }

        return conflicts;
    }

    private async Task<int> MergeBookmarksAsync(
        Guid profileId,
        string hostId,
        IReadOnlyList<StudentBookmark> remoteRows,
        CancellationToken cancellationToken)
    {
        Dictionary<string, StudentBookmark> localRows = (await _privateRepository
                .ListBookmarksForHostAsync(profileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(row => row.Id, StringComparer.Ordinal);
        int conflicts = 0;
        foreach (StudentBookmark remote in remoteRows.Where(row => string.Equals(row.HostId, hostId, StringComparison.Ordinal)))
        {
            if (!localRows.TryGetValue(remote.Id, out StudentBookmark? local))
            {
                await _privateRepository.SaveBookmarkAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
                continue;
            }

            MergeDecision decision = Decide(local.UpdatedUtc, remote.UpdatedUtc, SameBookmark(local, remote));
            if (decision == MergeDecision.UseRemote)
            {
                await _privateRepository.SaveBookmarkAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
            }
            else if (decision == MergeDecision.Conflict)
            {
                conflicts++;
            }
        }

        return conflicts;
    }

    private async Task<int> MergeAiHistoryAsync(
        Guid profileId,
        string hostId,
        IReadOnlyList<StudentAiHistoryEntry> remoteRows,
        CancellationToken cancellationToken)
    {
        Dictionary<string, StudentAiHistoryEntry> localRows = (await _privateRepository
                .ListAiHistoryAsync(profileId, hostId, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(row => row.Id, StringComparer.Ordinal);
        int conflicts = 0;
        foreach (StudentAiHistoryEntry remote in remoteRows.Where(row => string.Equals(row.HostId, hostId, StringComparison.Ordinal)))
        {
            if (!localRows.TryGetValue(remote.Id, out StudentAiHistoryEntry? local))
            {
                await _privateRepository.SaveAiHistoryAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
                continue;
            }

            MergeDecision decision = Decide(local.CreatedUtc, remote.CreatedUtc, SameAiHistory(local, remote));
            if (decision == MergeDecision.UseRemote)
            {
                await _privateRepository.SaveAiHistoryAsync(profileId, remote, cancellationToken).ConfigureAwait(false);
            }
            else if (decision == MergeDecision.Conflict)
            {
                conflicts++;
            }
        }

        return conflicts;
    }

    private static MergeDecision Decide(DateTimeOffset localUpdated, DateTimeOffset remoteUpdated, bool sameContent)
    {
        TimeSpan delta = remoteUpdated - localUpdated;
        if (delta.Duration() <= TimeSpan.FromSeconds(1))
        {
            return sameContent ? MergeDecision.KeepLocal : MergeDecision.Conflict;
        }

        return delta > TimeSpan.Zero ? MergeDecision.UseRemote : MergeDecision.KeepLocal;
    }

    private static bool SameAnnotation(StudentAnnotation left, StudentAnnotation right) =>
        string.Equals(left.HostId, right.HostId, StringComparison.Ordinal) &&
        string.Equals(left.BookId, right.BookId, StringComparison.Ordinal) &&
        left.PageNumber == right.PageNumber &&
        string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
        string.Equals(left.Color, right.Color, StringComparison.Ordinal) &&
        string.Equals(left.Body, right.Body, StringComparison.Ordinal) &&
        left.CreatedUtc == right.CreatedUtc &&
        left.IsDeleted == right.IsDeleted;

    private static bool SameBookmark(StudentBookmark left, StudentBookmark right) =>
        string.Equals(left.HostId, right.HostId, StringComparison.Ordinal) &&
        string.Equals(left.BookId, right.BookId, StringComparison.Ordinal) &&
        left.PageNumber == right.PageNumber &&
        string.Equals(left.Label, right.Label, StringComparison.Ordinal) &&
        left.CreatedUtc == right.CreatedUtc &&
        left.IsDeleted == right.IsDeleted;

    private static bool SameAiHistory(StudentAiHistoryEntry left, StudentAiHistoryEntry right) =>
        string.Equals(left.HostId, right.HostId, StringComparison.Ordinal) &&
        string.Equals(left.Query, right.Query, StringComparison.Ordinal) &&
        string.Equals(left.ResponseSummary, right.ResponseSummary, StringComparison.Ordinal) &&
        string.Equals(left.Tier, right.Tier, StringComparison.Ordinal) &&
        left.IsDeleted == right.IsDeleted;

    private enum MergeDecision
    {
        KeepLocal,
        UseRemote,
        Conflict,
    }
}
