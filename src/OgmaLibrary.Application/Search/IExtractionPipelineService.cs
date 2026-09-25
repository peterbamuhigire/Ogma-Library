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

/// <summary>
/// Optional rebuild capability that writes chunks under a staging index
/// version, allowing the active full-text index to remain readable.
/// </summary>
public interface IStagedExtractionPipelineService
{
    /// <summary>Indexes a batch into the supplied staging index version.</summary>
    Task<ExtractionBatchResult> IndexNextBatchAsync(
        int maxBooks,
        string indexVersion,
        CancellationToken cancellationToken);
}

/// <summary>Result of indexing one book.</summary>
/// <param name="BookId">The book.</param>
/// <param name="Succeeded">Whether indexing completed.</param>
/// <param name="PagesProcessed">Pages extracted in this run.</param>
/// <param name="PagesSkipped">Pages reused from an earlier run.</param>
/// <param name="FailedPages">Pages that failed extraction.</param>
/// <param name="ChunksWritten">Search chunks written.</param>
/// <param name="ErrorMessage">A redacted error message.</param>
/// <param name="IsPermanent">
/// True when retrying cannot help (for example the PDF is password-protected or the book was
/// removed), so the job runtime must not retry it (Sept-23 Phase 06, T06.1).
/// </param>
public sealed record ExtractionBookResult(
    string BookId,
    bool Succeeded,
    int PagesProcessed,
    int PagesSkipped,
    int FailedPages,
    int ChunksWritten,
    string? ErrorMessage,
    bool IsPermanent = false);

/// <summary>Result of one pending-book indexing batch.</summary>
public sealed record ExtractionBatchResult(
    int BooksAttempted,
    int BooksIndexed,
    int BooksFailed,
    int PagesProcessed,
    int PagesSkipped,
    int FailedPages,
    int ChunksWritten);
