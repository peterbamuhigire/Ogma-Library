using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Coordinates staged embedding generation with the durable active-index
/// pointer. A cancelled or unavailable rebuild retains its staging token for
/// the next invocation and never disturbs the active generation.
/// </summary>
public sealed class EmbeddingIndexRebuildService : IEmbeddingIndexRebuildService
{
    private readonly IStagedEmbeddingGenerationService _generation;
    private readonly IEmbeddingIndexLifecycleService _lifecycle;

    /// <summary>Initializes the rebuild coordinator.</summary>
    public EmbeddingIndexRebuildService(
        IStagedEmbeddingGenerationService generation,
        IEmbeddingIndexLifecycleService lifecycle)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(lifecycle);
        _generation = generation;
        _lifecycle = lifecycle;
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexRebuildResult> RebuildAsync(
        int maxChunks,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunks);

        EmbeddingIndexState state = await _lifecycle.GetStateAsync(cancellationToken)
            .ConfigureAwait(false);
        string stagingVersion = state.StagingIndexVersion ?? $"semantic-rebuild-{Guid.NewGuid():N}";
        state = await _lifecycle.BeginRebuildAsync(stagingVersion, cancellationToken)
            .ConfigureAwait(false);

        int attempted = 0;
        int embedded = 0;
        int failed = 0;
        while (true)
        {
            EmbeddingGenerationBatchResult batch = await _generation
                .GenerateNextBatchAsync(maxChunks, stagingVersion, cancellationToken)
                .ConfigureAwait(false);
            attempted += batch.ChunksAttempted;
            embedded += batch.ChunksEmbedded;
            failed += batch.ChunksFailed;

            if (batch.ProviderUnavailable || batch.ChunksFailed > 0)
            {
                return new EmbeddingIndexRebuildResult(
                    Completed: false,
                    state.ActiveIndexVersion,
                    stagingVersion,
                    attempted,
                    embedded,
                    failed,
                    batch.ProviderUnavailable);
            }

            if (batch.ChunksAttempted == 0)
            {
                EmbeddingIndexState promoted = await _lifecycle
                    .PromoteAsync(stagingVersion, cancellationToken)
                    .ConfigureAwait(false);
                return new EmbeddingIndexRebuildResult(
                    Completed: true,
                    promoted.ActiveIndexVersion,
                    promoted.StagingIndexVersion,
                    attempted,
                    embedded,
                    failed,
                    ProviderUnavailable: false);
            }
        }
    }
}
