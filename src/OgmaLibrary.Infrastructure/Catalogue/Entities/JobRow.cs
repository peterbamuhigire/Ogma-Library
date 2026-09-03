namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a background job (HLD §3 — Jobs table,
/// NFR-OGMA-009). The <see cref="IdempotencyKey"/> unique index guarantees that
/// duplicate job submissions are detected and rejected.
/// </summary>
public sealed class JobRow
{
    /// <summary>The stable job identifier.</summary>
    public long JobId { get; set; }

    /// <summary>The job type name (e.g. "ThumbnailGeneration", "HashComputation").</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// The idempotency key that uniquely identifies this logical work item.
    /// A unique constraint prevents duplicate submissions (NFR-OGMA-009).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Job status (0=Pending, 1=Running, 2=Completed, 3=Failed, 4=Cancelled, 5=DeadLetter).</summary>
    public int Status { get; set; }

    /// <summary>Optional FK to the book this job operates on.</summary>
    public string? BookId { get; set; }

    /// <summary>JSON payload carrying job-specific parameters.</summary>
    public string? Payload { get; set; }

    /// <summary>UTC timestamp when this job was started.</summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>UTC timestamp when this job completed (successfully or not).</summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Error message if the job failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Number of retry attempts so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>Current worker lease owner.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>UTC lease expiry; expired leases are recoverable.</summary>
    public DateTimeOffset? LeaseExpiresUtc { get; set; }

    /// <summary>Earliest UTC time at which a retry may be claimed.</summary>
    public DateTimeOffset? NextAttemptUtc { get; set; }

    /// <summary>Stable machine-readable failure code.</summary>
    public string? FailureCode { get; set; }
}
