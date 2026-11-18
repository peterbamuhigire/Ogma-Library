using System.Diagnostics;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// Measures UI-thread responsiveness for the real-window harness (Sept-23 K71). Active only with
/// <c>OGMA_E2E=1</c>: a background loop posts an empty input-priority job to the UI thread every
/// 250 ms and logs <c>ui.stall</c> when the round trip exceeds one second, naming the startup
/// phase. A stalled UI thread cannot process a window close, so this explains slow exits.
/// </summary>
internal sealed class UiResponsivenessProbe : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StallThreshold = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger;
    private readonly Func<string> _phase;
    private readonly CancellationTokenSource _stop = new();

    private UiResponsivenessProbe(ILogger logger, Func<string> phase)
    {
        _logger = logger;
        _phase = phase;
    }

    /// <summary>Starts the probe when the E2E environment asks for it; otherwise returns null.</summary>
    public static UiResponsivenessProbe? StartIfRequested(ILogger logger, Func<string> phase)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("OGMA_E2E"), "1", StringComparison.Ordinal))
        {
            return null;
        }

        var probe = new UiResponsivenessProbe(logger, phase);
        _ = Task.Run(probe.RunAsync);
        return probe;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        CancellationToken token = _stop.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(Interval, token).ConfigureAwait(false);
                var watch = Stopwatch.StartNew();
                string phaseAtStart = _phase();
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Input).GetTask()
                    .WaitAsync(token).ConfigureAwait(false);
                if (watch.Elapsed >= StallThreshold)
                {
                    AppLog.UiStalled(_logger, (int)watch.Elapsed.TotalMilliseconds, phaseAtStart, _phase());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is exiting.
        }
        catch (ObjectDisposedException)
        {
            // The application is exiting.
        }
    }
}
