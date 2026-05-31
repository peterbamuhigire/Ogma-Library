namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Aggregates scan health data from the Jobs table and catalogue for the V1
/// scan health report panel (FR-LIB-007).
/// </summary>
public interface IScanHealthService
{
    /// <summary>
    /// Returns the current scan health report, grouping failures into four
    /// actionable categories.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<ScanHealthReport> GetReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues all failed jobs (sets their status back to Pending) so the
    /// background worker picks them up again.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RetryAllFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues a single failed job by its identifier.
    /// </summary>
    /// <param name="jobId">The Jobs row identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default);
}
