using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Path-redacted facts about one library file of a book, for comparing copies and editions side by
/// side (Sept-23 Phase 10, task 10.9, CAT-007). Desktop only; never expose in LAN projections.
/// </summary>
/// <param name="BookId">The book the file belongs to.</param>
/// <param name="Title">The book title, or null when not yet known.</param>
/// <param name="RelativePath">Library-root-relative path of the file.</param>
/// <param name="SizeBytes">File size in bytes, when known.</param>
/// <param name="Sha256Prefix">The first characters of the content hash, or null.</param>
/// <param name="Validity">Discovery-time validity of the file.</param>
/// <param name="IsPresent">Whether the file is currently on disk.</param>
/// <param name="AddedUtc">When the book was first registered, when known.</param>
/// <param name="LastSeenUtc">When the file was last seen on disk.</param>
public sealed record BookFileFacts(
    string BookId,
    string? Title,
    string RelativePath,
    long? SizeBytes,
    string? Sha256Prefix,
    FileValidity Validity,
    bool IsPresent,
    DateTimeOffset? AddedUtc,
    DateTimeOffset LastSeenUtc);

/// <summary>
/// Read-only bridge between catalogue books and their canonical file occurrences, used by the
/// identity grouping UI (Sept-23 Phase 10, task 10.9). It never deletes or moves files.
/// </summary>
public interface IBookOccurrenceReader
{
    /// <summary>
    /// Returns the canonical file occurrence ids of each book. Books registered since the last
    /// identity backfill are backfilled first, so a freshly scanned book can be grouped.
    /// </summary>
    /// <param name="bookIds">The book ids.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Occurrence ids per book id; a book without files maps to an empty list.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveOccurrenceIdsAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the files of the books, and of every book that shares an active identity group with
    /// them, ordered by book and path.
    /// </summary>
    /// <param name="bookIds">The book ids.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file facts.</returns>
    Task<IReadOnlyList<BookFileFacts>> GetFileFactsAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default);
}
