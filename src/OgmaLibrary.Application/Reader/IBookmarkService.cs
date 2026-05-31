using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Creates, renames, deletes, and lists durable bookmarks (FR-READ-007, NFR-OGMA-008).
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Creates a bookmark on the given page. Persists before returning
    /// (NFR-OGMA-008). A <see langword="null"/> label means the presentation layer
    /// should render its localized default page label.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="label">Optional user-supplied label.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted bookmark.</returns>
    Task<Bookmark> CreateAsync(
        string bookId,
        int pageIndex,
        string? label,
        CancellationToken cancellationToken);

    /// <summary>Renames an existing bookmark.</summary>
    /// <param name="bookmarkId">The stable bookmark identifier.</param>
    /// <param name="newLabel">The new label text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken);

    /// <summary>Deletes an existing bookmark.</summary>
    /// <param name="bookmarkId">The stable bookmark identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken);

    /// <summary>Returns all bookmarks for a book, ordered by page number.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The bookmarks.</returns>
    Task<IReadOnlyList<Bookmark>> GetForBookAsync(
        string bookId,
        CancellationToken cancellationToken);
}
