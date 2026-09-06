namespace OgmaLibrary.Application.Ingestion;

/// <summary>Durable lifecycle values for the legacy job queue.</summary>
public enum JobRuntimeStatus
{
    /// <summary>Waiting for an eligible worker.</summary>
    Pending = 0,

    /// <summary>Owned by a worker lease.</summary>
    Running = 1,

    /// <summary>Completed successfully.</summary>
    Completed = 2,

    /// <summary>Failed after retry policy evaluation.</summary>
    Failed = 3,

    /// <summary>Cancelled without deleting history.</summary>
    Cancelled = 4,

    /// <summary>Quarantined because retrying cannot safely make progress.</summary>
    DeadLetter = 5,
}

/// <summary>Safe lease handed to one worker for one job.</summary>
public sealed record JobLease(
    long JobId,
    string JobType,
    string? BookId,
    string? Payload,
    int Attempt,
    string LeaseOwner,
    DateTimeOffset LeaseExpiresUtc);

/// <summary>Typed failure result used by the job runtime.</summary>
public sealed record JobFailure(
    string Code,
    string? SafeMessage,
    bool Retryable,
    bool DeadLetter = false);

/// <summary>Read-only operational snapshot of the durable job runtime.</summary>
public sealed record JobRuntimeMetrics(
    DateTimeOffset CapturedUtc,
    int PendingCount,
    int RunningCount,
    int CompletedCount,
    int FailedCount,
    int CancelledCount,
    int DeadLetterCount,
    int TotalAttempts,
    IReadOnlyDictionary<string, int> ActiveByJobType);

/// <summary>Safe per-job diagnostic projection with no payload or secret fields.</summary>
public sealed record JobRuntimeDiagnostic(
    long JobId,
    string JobType,
    JobRuntimeStatus Status,
    int Attempt,
    string? FailureCode,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc);

/// <summary>Exportable job diagnostics composed only of operational fields.</summary>
public sealed record JobRuntimeDiagnostics(
    JobRuntimeMetrics Metrics,
    IReadOnlyList<JobRuntimeDiagnostic> RecentJobs);

/// <summary>Durable claim/complete/failure contract for background jobs.</summary>
public interface IJobRuntimeService
{
    /// <summary>Atomically claims the oldest due job from the supplied type set.</summary>
    Task<JobLease?> ClaimNextAsync(
        IReadOnlyCollection<string> jobTypes,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a job only when the worker owns its active lease.</summary>
    Task CompleteAsync(
        long jobId,
        string workerId,
        CancellationToken cancellationToken = default);

    /// <summary>Renews an active lease only when the worker still owns it.</summary>
    Task RenewAsync(
        long jobId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Records a redacted failure and schedules retry or terminal failure.</summary>
    Task FailAsync(
        long jobId,
        string workerId,
        JobFailure failure,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels queued work without deleting its history. An actively leased job
    /// cannot be reported as cancelled until its handler supports cooperative
    /// cancellation at a safe checkpoint.
    /// </summary>
    Task CancelPendingAsync(
        long jobId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns expired running jobs to the queue for deterministic recovery.</summary>
    Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns status totals and active-lease metrics without exposing job payloads.</summary>
    Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>Exports a bounded JSON diagnostic snapshot without job payloads or error text.</summary>
    Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default);
}
