using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OgmaLibrary.Workers.Ocr;

/// <summary>
/// Background worker for Phase 15 OCR jobs. The processor owns idempotency and
/// resume behavior; this worker only schedules work away from the UI thread, through the
/// shared guarded loop (Sept-23 Phase 06, T06.5).
/// </summary>
internal sealed class OcrWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private readonly IOcrJobProcessor _processor;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of <see cref="OcrWorker"/>.</summary>
    /// <param name="processor">The OCR job processor.</param>
    /// <param name="logger">Optional logger.</param>
    public OcrWorker(IOcrJobProcessor processor, ILogger<OcrWorker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ResilientJobLoop.RunAsync(
            nameof(OcrWorker),
            token => _processor.ProcessNextAsync(token),
            IdleDelay,
            _logger,
            stoppingToken);
}
