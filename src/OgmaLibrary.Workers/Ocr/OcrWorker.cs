using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ocr;

namespace OgmaLibrary.Workers.Ocr;

/// <summary>
/// Background worker for Phase 15 OCR jobs. The processor owns idempotency and
/// resume behavior; this worker only schedules work away from the UI thread, through the
/// shared guarded loop (Sept-23 Phase 06, T06.5). When idle it runs the automatic OCR policy
/// (Sept-23 Phase 17, task 3) at most every <see cref="SweepInterval"/>.
/// </summary>
internal sealed class OcrWorker : BackgroundService
{
    /// <summary>Minimum time between automatic OCR policy sweeps.</summary>
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private readonly IOcrJobProcessor _processor;
    private readonly IOcrAutoPolicy? _autoPolicy;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private DateTimeOffset _nextSweep = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of <see cref="OcrWorker"/>.</summary>
    /// <param name="processor">The OCR job processor.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="autoPolicy">Optional automatic OCR policy.</param>
    /// <param name="time">Optional clock for tests.</param>
    public OcrWorker(
        IOcrJobProcessor processor,
        ILogger<OcrWorker>? logger = null,
        IOcrAutoPolicy? autoPolicy = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
        _autoPolicy = autoPolicy;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ResilientJobLoop.RunAsync(
            nameof(OcrWorker),
            RunOnceAsync,
            IdleDelay,
            _logger,
            stoppingToken);

    /// <summary>Processes one OCR job or, when none is due, runs the auto policy sweep.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when work was done.</returns>
    internal async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (await _processor.ProcessNextAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (_autoPolicy is null || now < _nextSweep)
        {
            return false;
        }

        _nextSweep = now.Add(SweepInterval);
        int queued = await _autoPolicy.SweepAsync(cancellationToken).ConfigureAwait(false);
        return queued > 0;
    }
}
