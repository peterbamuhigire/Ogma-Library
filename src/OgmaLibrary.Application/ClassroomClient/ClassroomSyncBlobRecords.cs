namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Plain private-state snapshot before sync compression/encryption.</summary>
public sealed record ClassroomSyncSnapshot(
    Guid ProfileId,
    string HostId,
    DateTimeOffset ExportedUtc,
    IReadOnlyList<StudentReadingProgress> ReadingProgress,
    IReadOnlyList<StudentAnnotation> Annotations,
    IReadOnlyList<StudentBookmark> Bookmarks,
    IReadOnlyList<StudentAiHistoryEntry> AiHistory,
    StudentSyncState? SyncState);

/// <summary>Encrypted private-state sync payload safe for opaque Host storage.</summary>
public sealed record EncryptedClassroomSyncBlob(
    int Version,
    string ContentType,
    byte[] Content);
