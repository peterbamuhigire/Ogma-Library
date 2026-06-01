using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 10 search indexing. It polls for pending or stale
/// books and delegates all idempotency to <see cref="IExtractionPipelineService"/>.
/// </summary>
public sealed class SearchExtractionWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);
    private readonly IExtractionPipelineService _pipeline;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchExtractionWorker"/>.
    /// </summary>
    public SearchExtractionWorker(IExtractionPipelineService pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ExtractionBatchResult result = await _pipeline
                    .IndexNextBatchAsync(maxBooks: 3, stoppingToken)
                    .ConfigureAwait(false);

                if (result.BooksAttempted == 0)
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
