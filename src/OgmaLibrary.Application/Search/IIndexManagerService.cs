namespace OgmaLibrary.Application.Search;

/// <summary>
/// Backend contract for the Phase 10 Index Manager dashboard and rebuild action.
/// </summary>
public interface IIndexManagerService
{
    /// <summary>
    /// Emits status updates after status reads and rebuild lifecycle changes.
    /// </summary>
    IObservable<IndexStatusUpdate> Events { get; }

    /// <summary>
    /// Returns current index counts and per-book indexing status.
    /// </summary>
    Task<IndexManagerStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rebuilds the derived search index from source rows.
    /// </summary>
    Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken);
}

/// <summary>Current status summary for the Index Manager.</summary>
public sealed record IndexManagerStatus(
    int TotalBooks,
    int IndexedBooks,
    int ExtractingBooks,
    int FailedBooks,
    int PendingOcrPages,
    int FailedExtractionPages,
    int SearchChunkCount,
    long IndexSizeBytes,
    FtsIntegrityResult Integrity,
    IReadOnlyList<BookIndexStatusItem> Books);

/// <summary>Per-book row for the Index Manager status list.</summary>
public sealed record BookIndexStatusItem(
    string BookId,
    string? Title,
    SearchBookIndexStatus Status,
    int ExtractedPageCount,
    int SearchChunkCount,
    int FailedPageCount,
    int PendingOcrPageCount);

/// <summary>Result of a rebuild attempt.</summary>
public sealed record IndexRebuildResult(
    bool Completed,
    int BooksAttempted,
    int BooksIndexed,
    int BooksFailed,
    int ChunksWritten,
    bool IntegrityHealthy,
    string? ErrorMessage);

/// <summary>Index Manager event payload.</summary>
public abstract record IndexStatusUpdate
{
    /// <summary>Status snapshot event.</summary>
    public sealed record StatusChanged(IndexManagerStatus Status) : IndexStatusUpdate;

    /// <summary>Rebuild started event.</summary>
    public sealed record RebuildStarted(DateTimeOffset StartedAtUtc) : IndexStatusUpdate;

    /// <summary>Rebuild completed event.</summary>
    public sealed record RebuildCompleted(IndexRebuildResult Result) : IndexStatusUpdate;
}
