namespace OgmaLibrary.Application.Search;

/// <summary>
/// Repository for search chunks that feed the local FTS5 index and Phase 11
/// embeddings. Implementations must keep chunk writes idempotent for rebuild
/// and resume workflows (NFR-OGMA-009).
/// </summary>
public interface ISearchChunkRepository
{
    /// <summary>
    /// Replaces chunks for one book and source category inside one transaction.
    /// </summary>
    /// <param name="bookId">The stable catalogue book identity.</param>
    /// <param name="source">The source category being replaced.</param>
    /// <param name="chunks">The replacement chunk set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted replacement chunks, including database identifiers.</returns>
    Task<IReadOnlyList<SearchChunkRecord>> ReplaceForBookAsync(
        string bookId,
        SearchChunkSource source,
        IReadOnlyList<SearchChunkRecord> chunks,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all chunks for one book ordered by source, page, and chunk index.
    /// </summary>
    /// <param name="bookId">The stable catalogue book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The book's stored search chunks.</returns>
    Task<IReadOnlyList<SearchChunkRecord>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts all stored search chunks.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of stored chunks.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken);
}
