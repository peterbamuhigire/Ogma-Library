namespace OgmaLibrary.Application;

/// <summary>
/// Abstracts wall-clock timing for performance-budget enforcement (NFR-OGMA-001..007).
/// Domain and application services accept this contract via constructor injection so
/// CI benchmark tests can inject a recording context and assert that operations
/// complete within their budget — without service code referencing
/// <see cref="System.Diagnostics.Stopwatch"/> directly.
/// </summary>
public interface IBenchmarkContext
{
    /// <summary>
    /// Starts a named timing scope. Dispose the returned handle to stop the timer
    /// and record the elapsed duration against <paramref name="operationName"/>.
    /// </summary>
    /// <param name="operationName">The operation being measured.</param>
    /// <returns>A handle whose disposal stops and records the measurement.</returns>
    IDisposable Measure(string operationName);

    /// <summary>Returns the last recorded duration for the named operation.</summary>
    /// <param name="operationName">The operation to query.</param>
    /// <returns>The last recorded duration, or <see cref="TimeSpan.Zero"/> if none.</returns>
    TimeSpan GetLastDuration(string operationName);
}
