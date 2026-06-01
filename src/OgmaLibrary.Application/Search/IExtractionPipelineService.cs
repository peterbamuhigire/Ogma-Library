namespace OgmaLibrary.Application.Search;

/// <summary>
/// Runs the Phase 10 extraction pipeline that turns catalogue books into
/// extracted pages and FTS-backed search chunks. Implementations must be
/// idempotent so interrupted runs can resume without duplicate chunks.
/// </summary>
public interface IExtractionPipelineService
{
    /// <summary>
    /// Extracts and indexes one book by its stable catalogue identity.
    /// </summary>
    Task<ExtractionBookResult> IndexBookAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds pending or stale books and indexes up to <paramref name="maxBooks"/>.
    /// </summary>
    Task<ExtractionBatchResult> IndexNextBatchAsync(
        int maxBooks,
        CancellationToken cancellationToken);
}

/// <summary>Result of indexing one book.</summary>
public sealed record ExtractionBookResult(
    string BookId,
    bool Succeeded,
    int PagesProcessed,
    int PagesSkipped,
    int FailedPages,
    int ChunksWritten,
    string? ErrorMessage);

/// <summary>Result of one pending-book indexing batch.</summary>
public sealed record ExtractionBatchResult(
    int BooksAttempted,
    int BooksIndexed,
    int BooksFailed,
    int PagesProcessed,
    int PagesSkipped,
    int FailedPages,
    int ChunksWritten);
