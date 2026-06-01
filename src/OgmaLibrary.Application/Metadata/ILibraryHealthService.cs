namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// A duplicate book entry found during health analysis (FR-META-007 health section).
/// </summary>
/// <param name="BookId">The catalogue book identifier.</param>
/// <param name="Title">The book title, if known.</param>
/// <param name="DuplicateKey">The ISBN or SHA-256 hash that reveals the duplicate relationship.</param>
/// <param name="DuplicateKind">Either "ISBN" or "ContentHash".</param>
public sealed record DuplicateBookEntry(
    string BookId,
    string? Title,
    string DuplicateKey,
    string DuplicateKind);

/// <summary>
/// A book entry in the missing-cover section of the health dashboard.
/// </summary>
/// <param name="BookId">The catalogue book identifier.</param>
/// <param name="Title">The book title, if known.</param>
public sealed record MissingCoverEntry(string BookId, string? Title);

/// <summary>
/// A book entry in the missing-ISBN section of the health dashboard.
/// </summary>
/// <param name="BookId">The catalogue book identifier.</param>
/// <param name="Title">The book title, if known.</param>
public sealed record MissingIsbnEntry(string BookId, string? Title);

/// <summary>
/// A book entry in the unavailable-files section of the health dashboard.
/// </summary>
/// <param name="BookId">The catalogue book identifier.</param>
/// <param name="Title">The book title, if known.</param>
/// <param name="RelativePath">The last known relative path.</param>
public sealed record UnavailableFileEntry(string BookId, string? Title, string? RelativePath);

/// <summary>
/// A failed background job entry in the health dashboard.
/// </summary>
/// <param name="JobId">The stable job identifier.</param>
/// <param name="JobType">The type of job (e.g. "Enrich", "ThumbnailGeneration").</param>
/// <param name="BookId">The book this job was for, if applicable.</param>
/// <param name="ErrorMessage">The failure error message.</param>
/// <param name="FailedUtc">UTC timestamp when the job was marked failed.</param>
public sealed record FailedJobEntry(
    long JobId,
    string JobType,
    string? BookId,
    string? ErrorMessage,
    DateTimeOffset? FailedUtc);

/// <summary>
/// Aggregated state for one batch enrichment run.
/// </summary>
public sealed record BatchEnrichmentRunEntry(
    string BatchId,
    int TotalJobs,
    int PendingJobs,
    int RunningJobs,
    int CompletedJobs,
    int FailedJobs,
    int PausedJobs);

/// <summary>
/// An aggregated snapshot of library health across all five sections
/// (FR-META-007, power-librarian persona).
/// </summary>
/// <param name="Duplicates">Books that share an ISBN or content hash with another book.</param>
/// <param name="MissingCovers">Books without a cover sidecar file.</param>
/// <param name="MissingIsbns">Books without a validated ISBN.</param>
/// <param name="UnavailableFiles">Books whose file is no longer present.</param>
/// <param name="FailedJobs">Background jobs with Status = Failed.</param>
/// <param name="LoadedUtc">UTC timestamp when the snapshot was loaded.</param>
/// <param name="BatchEnrichmentRuns">Recoverable batch enrichment run summaries.</param>
public sealed record LibraryHealthSnapshot(
    IReadOnlyList<DuplicateBookEntry> Duplicates,
    IReadOnlyList<MissingCoverEntry> MissingCovers,
    IReadOnlyList<MissingIsbnEntry> MissingIsbns,
    IReadOnlyList<UnavailableFileEntry> UnavailableFiles,
    IReadOnlyList<FailedJobEntry> FailedJobs,
    DateTimeOffset LoadedUtc,
    IReadOnlyList<BatchEnrichmentRunEntry>? BatchEnrichmentRuns = null);

/// <summary>
/// Aggregates library health data across five health sections for the power-librarian
/// dashboard (FR-META-007). All five queries must complete in under 500 ms for a
/// 2,000-book library (NFR-PROD-003).
/// </summary>
public interface ILibraryHealthService
{
    /// <summary>
    /// Loads the library health snapshot by running five targeted queries concurrently.
    /// Total time must be less than 500 ms on a 2,000-book library.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="LibraryHealthSnapshot"/> for display in the dashboard.</returns>
    Task<LibraryHealthSnapshot> GetHealthSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-queues a failed job so the worker retries it (health dashboard Retry action).
    /// </summary>
    /// <param name="jobId">The failed job to retry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default);

    /// <summary>Pauses pending or running jobs for a recoverable batch enrichment run.</summary>
    Task PauseBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Resumes paused or failed jobs for a recoverable batch enrichment run.</summary>
    Task ResumeBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>Exports failed background jobs as CSV for operator review.</summary>
    Task<string> ExportFailedJobsCsvAsync(CancellationToken cancellationToken = default);
}
