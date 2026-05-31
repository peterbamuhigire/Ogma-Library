namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Thread-safe scan progress aggregator. Background workers call the mutating methods;
/// the UI subscribes to <see cref="ProgressChanged"/> and reads
/// <see cref="CurrentSnapshot"/> (NFR-PROD-005).
/// </summary>
public interface IScanProgressService
{
    /// <summary>The most recent progress snapshot.</summary>
    ScanProgressSnapshot CurrentSnapshot { get; }

    /// <summary>Raised on the calling thread when any progress value changes.</summary>
    event EventHandler<ScanProgressSnapshot>? ProgressChanged;

    /// <summary>Transitions to a new scan phase.</summary>
    /// <param name="phase">The new phase.</param>
    void SetPhase(ScanPhase phase);

    /// <summary>Increments the discovered-file count.</summary>
    void IncrementDiscovered();

    /// <summary>Increments the completed-file count.</summary>
    void IncrementCompleted();

    /// <summary>Increments the failed-file count.</summary>
    void IncrementFailed();

    /// <summary>Resets all counters (call before starting a new scan).</summary>
    void Reset();
}
