namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Owns per-profile private classroom state outside the standalone catalogue DB.</summary>
public interface IStudentPrivateRepository
{
    /// <summary>Gets the private database path that belongs to a profile.</summary>
    string GetPrivateDatabasePath(Guid profileId);

    /// <summary>Ensures the profile's private database exists and is schema-current.</summary>
    Task EnsureCreatedAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Gets private reading progress for one Host book.</summary>
    Task<StudentReadingProgress?> GetReadingProgressAsync(
        Guid profileId,
        string hostId,
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private reading progress for all books from one Host.</summary>
    Task<IReadOnlyList<StudentReadingProgress>> ListReadingProgressAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves private reading progress for one Host book.</summary>
    Task SaveReadingProgressAsync(
        Guid profileId,
        StudentReadingProgress progress,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private annotations for one Host book.</summary>
    Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsAsync(
        Guid profileId,
        string hostId,
        string bookId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private annotations for all books from one Host.</summary>
    Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsForHostAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a private annotation.</summary>
    Task SaveAnnotationAsync(
        Guid profileId,
        StudentAnnotation annotation,
        CancellationToken cancellationToken = default);

    /// <summary>Lists pending annotation conflicts for one Host.</summary>
    Task<IReadOnlyList<StudentAnnotationConflict>> ListAnnotationConflictsAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a pending annotation conflict.</summary>
    Task SaveAnnotationConflictAsync(
        Guid profileId,
        StudentAnnotationConflict conflict,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a pending annotation conflict after student choice.</summary>
    Task DeleteAnnotationConflictAsync(
        Guid profileId,
        string hostId,
        string annotationId,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a private annotation for future sync tombstones.</summary>
    Task SoftDeleteAnnotationAsync(
        Guid profileId,
        string annotationId,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private bookmarks for one Host book.</summary>
    Task<IReadOnlyList<StudentBookmark>> ListBookmarksAsync(
        Guid profileId,
        string hostId,
        string bookId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private bookmarks for all books from one Host.</summary>
    Task<IReadOnlyList<StudentBookmark>> ListBookmarksForHostAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a private bookmark.</summary>
    Task SaveBookmarkAsync(
        Guid profileId,
        StudentBookmark bookmark,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a private bookmark for future sync tombstones.</summary>
    Task SoftDeleteBookmarkAsync(
        Guid profileId,
        string bookmarkId,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Lists private AI history for one Host.</summary>
    Task<IReadOnlyList<StudentAiHistoryEntry>> ListAiHistoryAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a private AI history entry.</summary>
    Task SaveAiHistoryAsync(
        Guid profileId,
        StudentAiHistoryEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>Gets sync state for one Host.</summary>
    Task<StudentSyncState?> GetSyncStateAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves sync state for one Host.</summary>
    Task SaveSyncStateAsync(
        Guid profileId,
        StudentSyncState state,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the private database for a profile.</summary>
    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
