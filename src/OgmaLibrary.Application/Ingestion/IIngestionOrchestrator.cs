using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Coordinates the full ingestion pipeline: discovery → identity matching →
/// metadata extraction → asset generation jobs (FR-LIB-001..006).
/// </summary>
public interface IIngestionOrchestrator
{
    /// <summary>
    /// Scans every enabled library root. Returns when the pipeline has drained
    /// (all discovery results processed and jobs enqueued).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the scan.</param>
    /// <exception cref="OperationCanceledException">The scan was cancelled.</exception>
    Task ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the given roots (or every enabled root when null) and always leaves the
    /// scan progress in a terminal phase (Sept-23 Phase 05, T05.9). Cancellation is
    /// reported as <see cref="ScanOutcome.Cancelled"/>; unexpected errors set the
    /// <see cref="ScanPhase.Failed"/> phase and are rethrown.
    /// </summary>
    /// <param name="roots">The roots to scan, or null for every enabled root.</param>
    /// <param name="cancellationToken">A token to cancel the scan.</param>
    async Task<ScanSummary> ScanRootsAsync(
        IReadOnlyCollection<LibraryRootId>? roots,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ScanAsync(cancellationToken).ConfigureAwait(false);
            return ScanSummary.Empty(ScanOutcome.Completed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ScanSummary.Empty(ScanOutcome.Cancelled);
        }
    }
}
