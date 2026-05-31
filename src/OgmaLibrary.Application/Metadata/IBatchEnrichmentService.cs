namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// Per-book status within a batch enrichment run (FR-META-006).
/// </summary>
public enum BatchEnrichmentStatus
{
    /// <summary>The job is queued but not yet started.</summary>
    Queued = 0,

    /// <summary>The job is currently executing.</summary>
    Running = 1,

    /// <summary>Enrichment completed successfully for this book.</summary>
    Completed = 2,

    /// <summary>Enrichment failed; error details available.</summary>
    Failed = 3,
}

/// <summary>
/// Orchestrates batch enrichment for multiple books, creating one idempotent
/// background job per book and submitting them to the enrichment worker queue
/// (FR-META-006, NFR-OGMA-009).
/// </summary>
/// <remarks>
/// <para>
/// Rate limits are applied at the worker level via a token-bucket limiter.
/// The orchestrator is responsible only for job creation and idempotency.
/// </para>
/// </remarks>
public interface IBatchEnrichmentOrchestrator
{
    /// <summary>
    /// Creates enrichment jobs for each book in <paramref name="bookIds"/>.
    /// Books that already have a pending or completed enrichment job within the
    /// current session are skipped (idempotent).
    /// </summary>
    /// <param name="bookIds">The catalogue identifiers of books to enrich.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of new jobs created.</returns>
    Task<int> StartAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default);
}
