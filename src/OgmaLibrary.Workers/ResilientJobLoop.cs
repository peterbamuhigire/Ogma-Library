using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Workers;

/// <summary>
/// Shared loop for background job workers (Sept-23 Phase 06, T06.5, K26). An iteration that
/// throws is logged and followed by a capped exponential backoff (1 s, 2 s, 4 s … 30 s); the
/// loop never ends silently. Only the host's stopping token ends it. A cancellation that is
/// not the host stopping (for example an <c>HttpClient</c> timeout) is an ordinary failure.
/// </summary>
public static class ResilientJobLoop
{
    /// <summary>The first delay after a failed iteration.</summary>
    public static readonly TimeSpan InitialErrorDelay = TimeSpan.FromSeconds(1);

    /// <summary>The longest delay between failing iterations (the error-rate breaker).</summary>
    public static readonly TimeSpan MaxErrorDelay = TimeSpan.FromSeconds(30);

    /// <summary>Runs <paramref name="iteration"/> until <paramref name="stoppingToken"/> is cancelled.</summary>
    /// <param name="workerName">The worker name used in log events.</param>
    /// <param name="iteration">One unit of work; returns true when work was done (loop again at once).</param>
    /// <param name="idleDelay">The delay after an iteration that found no work.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stoppingToken">The host stopping token.</param>
    /// <param name="delay">Optional delay function (tests inject a fast clock).</param>
    /// <returns>A task that completes when the host stops.</returns>
    public static async Task RunAsync(
        string workerName,
        Func<CancellationToken, Task<bool>> iteration,
        TimeSpan idleDelay,
        ILogger logger,
        CancellationToken stoppingToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        ArgumentNullException.ThrowIfNull(iteration);
        ArgumentNullException.ThrowIfNull(logger);
        delay ??= Task.Delay;
        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                bool didWork = await iteration(stoppingToken).ConfigureAwait(false);
                consecutiveFailures = 0;
                wait = didWork ? TimeSpan.Zero : idleDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!ExceptionClassification.IsFatal(exception))
            {
                consecutiveFailures++;
                wait = ErrorDelay(consecutiveFailures);
                InfrastructureLog.WorkerRetryScheduled(logger, exception, workerName);
            }

            if (wait <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                await delay(wait, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Returns the capped exponential delay after <paramref name="consecutiveFailures"/> failures.</summary>
    /// <param name="consecutiveFailures">The number of consecutive failed iterations (1 or more).</param>
    /// <returns>The delay.</returns>
    public static TimeSpan ErrorDelay(int consecutiveFailures)
    {
        int exponent = Math.Clamp(consecutiveFailures - 1, 0, 10);
        double milliseconds = InitialErrorDelay.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaxErrorDelay.TotalMilliseconds));
    }
}
