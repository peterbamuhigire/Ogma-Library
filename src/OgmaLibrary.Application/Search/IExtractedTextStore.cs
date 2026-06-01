namespace OgmaLibrary.Application.Search;

/// <summary>
/// Durable store for per-page extracted text used by the Search Index bounded
/// context. Implementations own persistence mechanics; callers receive
/// EF-free records for LAN-safe projection and testability.
/// </summary>
public interface IExtractedTextStore
{
    /// <summary>
    /// Returns an extracted page for a book/page pair, or <see langword="null"/>
    /// when the page has not yet been extracted.
    /// </summary>
    /// <param name="bookId">The stable catalogue book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The extracted page record, or <see langword="null"/>.</returns>
    Task<ExtractedPageRecord?> GetPageAsync(
        string bookId,
        int pageIndex,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all extracted pages for a book ordered by page index.
    /// </summary>
    /// <param name="bookId">The stable catalogue book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The extracted pages for the book.</returns>
    Task<IReadOnlyList<ExtractedPageRecord>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates one extracted page inside a durable transaction.
    /// </summary>
    /// <param name="page">The page record to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted page record, including its database identifier.</returns>
    Task<ExtractedPageRecord> UpsertPageAsync(
        ExtractedPageRecord page,
        CancellationToken cancellationToken);
}
