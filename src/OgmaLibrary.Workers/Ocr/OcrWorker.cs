using Microsoft.Extensions.Hosting;

namespace OgmaLibrary.Workers.Ocr;

/// <summary>
/// Background worker for Phase 15 OCR jobs. The processor owns idempotency and
/// resume behavior; this worker only schedules work away from the UI thread.
/// </summary>
internal sealed class OcrWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(15);
    private readonly IOcrJobProcessor _processor;

    /// <summary>Initializes a new instance of <see cref="OcrWorker"/>.</summary>
    /// <param name="processor">The OCR job processor.</param>
    public OcrWorker(IOcrJobProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processed = await _processor.ProcessNextAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (!processed)
                {
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(ErrorDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
