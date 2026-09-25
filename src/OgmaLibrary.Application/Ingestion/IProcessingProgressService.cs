namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Background processing progress, counted in tasks (jobs), kept apart from scan progress,
/// which is counted in files (Sept-23 Phase 06, T06.6, K13). Counts cover the current batch:
/// the work queued since processing last became active.
/// </summary>
/// <param name="Queued">Tasks waiting to run (including retries waiting for their backoff).</param>
/// <param name="Running">Tasks running now.</param>
/// <param name="Done">Tasks of this batch that reached a terminal state.</param>
/// <param name="Failed">Tasks of this batch that failed terminally.</param>
/// <param name="WaitingForCapability">Tasks parked until a capability (for example semantic search) is set up; never "busy".</param>
/// <param name="WaitingByCapability">Parked tasks per capability name.</param>
/// <param name="AssetsCompleted">All-time completed cover/metadata tasks; a change means the catalogue has new visuals.</param>
public sealed record ProcessingSnapshot(
    int Queued,
    int Running,
    int Done,
    int Failed,
    int WaitingForCapability,
    IReadOnlyDictionary<string, int> WaitingByCapability,
    int AssetsCompleted)
{
    /// <summary>An idle snapshot.</summary>
    public static ProcessingSnapshot Idle { get; } =
        new(0, 0, 0, 0, 0, new Dictionary<string, int>(StringComparer.Ordinal), 0);

    /// <summary>True while tasks are queued or running. Parked tasks never make processing active.</summary>
    public bool IsActive => Queued + Running > 0;

    /// <summary>The batch total; never smaller than <see cref="Done"/>.</summary>
    public int Total => Done + Queued + Running;
}

/// <summary>
/// Publishes <see cref="ProcessingSnapshot"/> updates for the status bar and the progressive
/// catalogue refresh. Workers call <see cref="NotifyChanged"/> after claiming or finishing a
/// task; updates are coalesced.
/// </summary>
public interface IProcessingProgressService
{
    /// <summary>The latest snapshot.</summary>
    ProcessingSnapshot CurrentSnapshot { get; }

    /// <summary>Raised on a background thread when the snapshot changes.</summary>
    event EventHandler<ProcessingSnapshot>? ProgressChanged;

    /// <summary>Requests a coalesced refresh of the snapshot.</summary>
    void NotifyChanged();

    /// <summary>Recomputes the snapshot now.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The new snapshot.</returns>
    Task<ProcessingSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
