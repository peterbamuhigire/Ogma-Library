namespace OgmaLibrary.Application.Diagnostics;

/// <summary>
/// Marshals view-model and bound-collection mutations onto the UI thread (Sept-23 Phase 02, K72).
/// Library code continues with <c>ConfigureAwait(false)</c>; any state a view binds to is
/// published through this dispatcher. Implementations post and never block waiting for the UI.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Returns <see langword="true"/> when the caller is already on the UI thread.</summary>
    /// <returns>Whether UI state may be mutated synchronously.</returns>
    bool CheckAccess();

    /// <summary>Queues <paramref name="action"/> to run on the UI thread without waiting.</summary>
    /// <param name="action">The UI mutation to run.</param>
    void Post(Action action);

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread and completes when it has run. When the
    /// caller is already on the UI thread the action runs inline.
    /// </summary>
    /// <param name="action">The UI mutation to run.</param>
    /// <param name="cancellationToken">A token that abandons the wait (the action may still run).</param>
    /// <returns>A task that completes after the action ran.</returns>
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
}

/// <summary>
/// A dispatcher that runs every action inline on the calling thread. Used by unit tests and
/// headless composition where no UI thread exists.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    /// <summary>The shared instance.</summary>
    public static InlineUiDispatcher Instance { get; } = new();

    /// <inheritdoc />
    public bool CheckAccess() => true;

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }
}
