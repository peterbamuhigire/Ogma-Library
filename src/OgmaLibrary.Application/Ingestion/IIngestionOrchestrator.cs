namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Coordinates the full ingestion pipeline: discovery → identity matching →
/// metadata extraction → asset generation jobs (FR-LIB-001..006).
/// </summary>
public interface IIngestionOrchestrator
{
    /// <summary>
    /// Starts a full scan of the library root configured in
    /// <see cref="ILibrarySettingsService"/>. Returns when the pipeline has drained
    /// (all discovery results processed and jobs enqueued).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the scan.</param>
    Task ScanAsync(CancellationToken cancellationToken = default);
}
