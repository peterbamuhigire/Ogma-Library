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
