namespace OgmaLibrary.Application.Ocr;

/// <summary>Queues OCR work for scanned or image-only PDF books.</summary>
public interface IOcrJobQueueService
{
    /// <summary>Queues or resumes OCR for a book.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="languageHint">OCR language key, such as <c>eng</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The queue result.</returns>
    Task<OcrQueueResult> QueueBookAsync(
        string bookId,
        string languageHint = "eng",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts catalogued books whose text status says OCR would make them searchable
    /// (image only or partly searchable) and that have no active OCR job (Sept-23 Phase 17).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of books.</returns>
    Task<int> CountBooksNeedingOcrAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    /// <summary>
    /// Queues OCR for up to <paramref name="maxBooks"/> books that need it ("Make N scanned
    /// books searchable"). Failed or cancelled jobs are queued again.
    /// </summary>
    /// <param name="languageHint">OCR language key.</param>
    /// <param name="maxBooks">Upper bound on the books queued by this call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of books queued.</returns>
    Task<int> QueueBooksNeedingOcrAsync(
        string languageHint = "eng",
        int maxBooks = 500,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

/// <summary>Result of an OCR queue request.</summary>
/// <param name="Queued">Whether a new or retry OCR job was queued.</param>
/// <param name="AlreadyQueued">Whether an existing active/completed job prevented duplicate queuing.</param>
/// <param name="JobId">The job id, when known.</param>
/// <param name="ErrorMessage">A user-displayable failure reason, when queuing failed.</param>
public sealed record OcrQueueResult(
    bool Queued,
    bool AlreadyQueued,
    long? JobId,
    string? ErrorMessage);
