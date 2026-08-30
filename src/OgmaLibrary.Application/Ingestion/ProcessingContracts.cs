using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>Lifecycle state of one durable scan session.</summary>
public enum ScanSessionStatus
{
    /// <summary>Session is accepting and processing stages.</summary>
    Running = 0,

    /// <summary>All required stages completed successfully.</summary>
    Completed = 1,

    /// <summary>At least one stage reached terminal failure.</summary>
    Failed = 2,

    /// <summary>Cancellation was requested; running work may drain.</summary>
    CancellationRequested = 3,

    /// <summary>Cancellation completed without deleting history.</summary>
    Cancelled = 4,
}

/// <summary>Lifecycle state of one independently retryable processing stage.</summary>
public enum StageExecutionStatus
{
    /// <summary>Stage is waiting for an eligible worker.</summary>
    Pending = 0,

    /// <summary>Stage is owned by a worker lease.</summary>
    Running = 1,

    /// <summary>Stage completed successfully.</summary>
    Completed = 2,

    /// <summary>Stage failed but may be retried.</summary>
    RetryableFailure = 3,

    /// <summary>Stage failed and cannot be retried automatically.</summary>
    TerminalFailure = 4,

    /// <summary>Stage was cancelled before completion.</summary>
    Cancelled = 5,
}

/// <summary>Durable scan session projection.</summary>
public sealed record ScanSessionDescriptor(
    long Id,
    LibraryRootId RootId,
    ScanSessionStatus Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    int TotalStages,
    int CompletedStages,
    int FailedStages);

/// <summary>Durable stage lease projection handed to a worker.</summary>
public sealed record StageExecutionLease(
    long Id,
    long ScanSessionId,
    string StageName,
    string SubjectKey,
    int Attempt,
    string LeaseOwner,
    DateTimeOffset LeaseExpiresUtc);

/// <summary>Safe, typed stage failure information. PDF text and provider secrets are excluded.</summary>
public sealed record StageFailure(
    string Code,
    string? SafeMessage,
    bool Retryable);

/// <summary>Durable scan-session and leased-stage orchestration contract.</summary>
public interface IProcessingStateService
{
    /// <summary>Starts a running session for an enabled root.</summary>
    Task<ScanSessionDescriptor> StartSessionAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an idempotent pending stage to a session.</summary>
    Task<long> EnqueueStageAsync(
        long scanSessionId,
        string stageName,
        string subjectKey,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically claims one eligible stage with a bounded lease.</summary>
    Task<StageExecutionLease?> ClaimNextAsync(
        string stageName,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a stage only when owned by the supplied worker.</summary>
    Task CompleteStageAsync(
        long stageExecutionId,
        string workerId,
        CancellationToken cancellationToken = default);

    /// <summary>Records a typed failure and schedules retry or terminal failure.</summary>
    Task FailStageAsync(
        long stageExecutionId,
        string workerId,
        StageFailure failure,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default);

    /// <summary>Requests cancellation without deleting session or stage history.</summary>
    Task RequestCancellationAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Expires crashed-worker leases back to pending state.</summary>
    Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes a session after evaluating all stage outcomes.</summary>
    Task<ScanSessionDescriptor> FinalizeSessionAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default);
}
