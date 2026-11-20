namespace OgmaLibrary.Application.Search;

/// <summary>
/// User control over background semantic indexing (Sept-23 Phase 14, task 14.7 groundwork).
/// Pausing stops new embedding batches and cancels the one in flight without consuming a
/// retry; it does not change the provider choice and is not persisted across restarts.
/// </summary>
public interface ISemanticIndexControl
{
    /// <summary>True while background embedding is paused.</summary>
    bool IsPaused { get; }

    /// <summary>
    /// Raised on a background thread whenever indexing is paused, resumed or asked to build
    /// now. UI consumers must marshal to their own thread.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Pauses background embedding.</summary>
    void Pause();

    /// <summary>Resumes background embedding.</summary>
    void ResumeIndexing();

    /// <summary>
    /// Resumes indexing, re-checks the semantic capability and wakes the embedding worker so
    /// parked jobs are resumed at once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the capability re-check.</param>
    /// <returns>A task that completes once the worker has been woken.</returns>
    Task BuildNowAsync(CancellationToken cancellationToken);
}
