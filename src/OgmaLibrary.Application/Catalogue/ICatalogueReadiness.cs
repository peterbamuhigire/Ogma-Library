namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Signals that the catalogue database schema is ready (Sept-23 stabilisation). On a fresh data
/// folder the tables exist only after the startup migration, so background consumers that may
/// start earlier (progress polling, log redaction) wait here instead of querying missing tables.
/// </summary>
public interface ICatalogueReadiness
{
    /// <summary>Gets a value indicating whether the catalogue migration has completed.</summary>
    bool IsReady { get; }

    /// <summary>Completes when the catalogue migration has completed.</summary>
    /// <param name="cancellationToken">A token that abandons the wait.</param>
    /// <returns>A task that completes once the catalogue is ready.</returns>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
}
