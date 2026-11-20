using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// Makes sure the reading position survives closing the window (READ-013). A page turn is saved
/// after a 500 ms pause, so a user who turns a page and closes the app straight away would lose it.
/// </summary>
public static class ReaderShutdown
{
    /// <summary>
    /// Closes the open book's session (which saves its position at once) and writes any debounced
    /// progress that has not been written yet. Never throws and never waits longer than
    /// <paramref name="budget"/>: shutdown must always finish.
    /// </summary>
    /// <remarks>
    /// This works on the session and progress services, not on <c>ReaderViewModel.CloseAsync</c>:
    /// exit waits for this call on the UI thread, and the view model's continuations return to it.
    /// </remarks>
    /// <param name="sessions">The main reader session service, or null when the reader never started.</param>
    /// <param name="progress">The reading progress service, or null.</param>
    /// <param name="budget">The longest time to spend.</param>
    /// <returns>True when everything was written within the budget.</returns>
    public static async Task<bool> FlushAsync(
        IReaderSessionService? sessions,
        IReadingProgressService? progress,
        TimeSpan budget)
    {
        using var cancellation = new CancellationTokenSource(budget);
        CancellationToken token = cancellation.Token;
        try
        {
            if (sessions is not null)
            {
                await sessions.CloseAsync(token).ConfigureAwait(false);
            }

            if (progress is not null)
            {
                await progress.FlushAsync(token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
#pragma warning disable CA1031 // Shutdown must complete even when the position cannot be saved.
        catch (Exception)
        {
            return false;
        }
#pragma warning restore CA1031
    }
}
