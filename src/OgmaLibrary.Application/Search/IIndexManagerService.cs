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

    /// <summary>Pauses a queued or running OCR job.</summary>
    Task PauseOcrJobAsync(long jobId, CancellationToken cancellationToken);

    /// <summary>Cancels an OCR job and prevents future worker pickup.</summary>
    Task CancelOcrJobAsync(long jobId, CancellationToken cancellationToken);

    /// <summary>Retries a failed, cancelled, or paused OCR job.</summary>
    Task RetryOcrJobAsync(long jobId, CancellationToken cancellationToken);
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
    IReadOnlyList<BookIndexStatusItem> Books,
    IReadOnlyList<OcrJobStatusItem> OcrJobs);

/// <summary>Per-book row for the Index Manager status list.</summary>
public sealed record BookIndexStatusItem(
    string BookId,
    string? Title,
    SearchBookIndexStatus Status,
    int ExtractedPageCount,
    int SearchChunkCount,
    int FailedPageCount,
    int PendingOcrPageCount);

/// <summary>Current state of a queued OCR job.</summary>
public sealed record OcrJobStatusItem(
    long JobId,
    string? BookId,
    string? Title,
    OcrJobState State,
    int ProcessedPages,
    int TotalPages,
    string? ErrorMessage)
{
    /// <summary>Whether a numeric page progress value is available.</summary>
    public bool HasPageProgress => TotalPages > 0;

    /// <summary>Percentage complete, rounded down, when total pages are known.</summary>
    public int PercentComplete =>
        TotalPages <= 0 ? 0 : Math.Clamp((int)Math.Floor(ProcessedPages * 100d / TotalPages), 0, 100);
}

/// <summary>Normalized OCR job states for the Index Manager.</summary>
public enum OcrJobState
{
    /// <summary>Queued but not yet started.</summary>
    Pending = 0,

    /// <summary>Currently being processed or recovering from a prior interruption.</summary>
    Running = 1,

    /// <summary>Finished successfully.</summary>
    Completed = 2,

    /// <summary>Failed and needs user action or retry.</summary>
    Failed = 3,

    /// <summary>Cancelled by the user or system.</summary>
    Cancelled = 4,

    /// <summary>Paused by the user and resumable through retry.</summary>
    Paused = 5,
}

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
