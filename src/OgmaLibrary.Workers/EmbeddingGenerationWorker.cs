using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 11 semantic embedding generation. It polls for
/// search chunks that do not yet have a current local embedding vector.
/// </summary>
public sealed class EmbeddingGenerationWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(15);
    private readonly IEmbeddingGenerationService _generation;

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationWorker"/>.
    /// </summary>
    public EmbeddingGenerationWorker(IEmbeddingGenerationService generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EmbeddingGenerationBatchResult result = await _generation
                    .GenerateNextBatchAsync(maxChunks: 16, stoppingToken)
                    .ConfigureAwait(false);

                if (result.ChunksAttempted == 0 || result.ProviderUnavailable)
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
