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

    /// <summary>Paused at a handler-defined safe checkpoint and eligible to resume.</summary>
    Paused = 6,

    /// <summary>
    /// Parked because a capability it needs (for example a local embedding provider) is not
    /// available. It is neither failed nor pending, consumes no attempts and resumes when the
    /// capability becomes available (Sept-23 Phase 06, T06.1).
    /// </summary>
    WaitingForCapability = 7,
}

/// <summary>How a job failure is treated by the retry policy (Sept-23 Phase 06, T06.1).</summary>
public enum JobFailureKind
{
    /// <summary>A transient failure that is retried with exponential backoff up to the attempt cap.</summary>
    Retryable = 0,

    /// <summary>A failure that cannot succeed by retrying (for example the file is not a PDF).</summary>
    Permanent = 1,

    /// <summary>A required capability is missing; the job waits without consuming an attempt.</summary>
    CapabilityMissing = 2,

    /// <summary>The job's subject disappeared (for example the book was removed) so the job is cancelled.</summary>
    Cancelled = 3,
}

/// <summary>Stable capability names used by <see cref="JobRuntimeStatus.WaitingForCapability"/> jobs.</summary>
public static class JobCapabilities
{
    /// <summary>A local semantic embedding provider (Phase 14 adds its setup).</summary>
    public const string SemanticEmbeddings = "semantic-embeddings";
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
/// <param name="Code">A stable machine-readable failure code.</param>
/// <param name="SafeMessage">A redacted operator message.</param>
/// <param name="Retryable">Whether the failure is transient (legacy flag; see <paramref name="Kind"/>).</param>
/// <param name="DeadLetter">Whether the job must be quarantined.</param>
/// <param name="Kind">The explicit failure kind; when null it is derived from <paramref name="Retryable"/>.</param>
/// <param name="Capability">The missing capability for <see cref="JobFailureKind.CapabilityMissing"/>.</param>
public sealed record JobFailure(
    string Code,
    string? SafeMessage,
    bool Retryable,
    bool DeadLetter = false,
    JobFailureKind? Kind = null,
    string? Capability = null)
{
    /// <summary>The effective failure kind used by the retry policy.</summary>
    public JobFailureKind EffectiveKind =>
        Kind ?? (Retryable && !DeadLetter ? JobFailureKind.Retryable : JobFailureKind.Permanent);

    /// <summary>Creates a failure that parks the job until <paramref name="capability"/> is available.</summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="capability">The missing capability name.</param>
    /// <param name="safeMessage">A redacted operator message.</param>
    /// <returns>The failure.</returns>
    public static JobFailure CapabilityMissing(string code, string capability, string? safeMessage = null) =>
        new(code, safeMessage, Retryable: false, Kind: JobFailureKind.CapabilityMissing, Capability: capability);

    /// <summary>Creates a failure that cancels the job because its subject no longer exists.</summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="safeMessage">A redacted operator message.</param>
    /// <returns>The failure.</returns>
    public static JobFailure Cancelled(string code, string? safeMessage = null) =>
        new(code, safeMessage, Retryable: false, Kind: JobFailureKind.Cancelled);
}

/// <summary>Read-only operational snapshot of the durable job runtime.</summary>
public sealed record JobRuntimeMetrics(
    DateTimeOffset CapturedUtc,
    int PendingCount,
    int RunningCount,
    int CompletedCount,
    int FailedCount,
    int CancelledCount,
    int DeadLetterCount,
    int PausedCount,
    int TotalAttempts,
    IReadOnlyDictionary<string, int> ActiveByJobType,
    int WaitingForCapabilityCount = 0);

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

    /// <summary>Returns a terminal failed job to the queue and records the operator action.</summary>
    Task RetryFailedAsync(
        long jobId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns expired running jobs to the queue for deterministic recovery.</summary>
    Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a leased job to the queue without recording a failure or consuming an attempt,
    /// for example when the application shuts down mid-job (Sept-23 Phase 06, T06.2).
    /// </summary>
    /// <param name="jobId">The job.</param>
    /// <param name="workerId">The worker that owns the lease.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the lease is released.</returns>
    Task ReleaseAsync(
        long jobId,
        string workerId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Claims the oldest due job of the given types for one book, used to process a book's
    /// jobs as one batch against a shared sandbox copy (Sept-23 Phase 06, T06.10).
    /// </summary>
    /// <param name="jobTypes">The eligible job types.</param>
    /// <param name="bookId">The book whose jobs are claimed.</param>
    /// <param name="workerId">The claiming worker.</param>
    /// <param name="leaseDuration">The lease duration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The lease, or null when the book has no due job of these types.</returns>
    Task<JobLease?> ClaimNextForBookAsync(
        IReadOnlyCollection<string> jobTypes,
        string bookId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) => Task.FromResult<JobLease?>(null);

    /// <summary>
    /// Returns every job parked for <paramref name="capability"/> to the queue, for example when
    /// an embedding provider becomes healthy (Sept-23 Phase 06, T06.1).
    /// </summary>
    /// <param name="capability">The capability that became available.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of resumed jobs.</returns>
    Task<int> ResumeWaitingAsync(
        string capability,
        CancellationToken cancellationToken = default) => Task.FromResult(0);

    /// <summary>Returns status totals and active-lease metrics without exposing job payloads.</summary>
    Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a bounded, payload-free operational snapshot for user interfaces.</summary>
    Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(
        int recentJobLimit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Exports a bounded JSON diagnostic snapshot without job payloads or error text.</summary>
    Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default);
}
