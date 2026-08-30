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
    bool Retryable);

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

    /// <summary>Records a redacted failure and schedules retry or terminal failure.</summary>
    Task FailAsync(
        long jobId,
        string workerId,
        JobFailure failure,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default);

    /// <summary>Returns expired running jobs to the queue for deterministic recovery.</summary>
    Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default);
}
