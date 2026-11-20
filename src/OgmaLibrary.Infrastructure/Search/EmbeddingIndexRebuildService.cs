using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Coordinates staged embedding generation with the durable active-index
/// pointer. A cancelled or unavailable rebuild retains its staging token for
/// the next invocation and never disturbs the active generation. This is the
/// only path that promotes a staging generation (Sept-23 Phase 14, slice 4).
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

        int attempted = 0;
        int embedded = 0;
        int failed = 0;
        while (true)
        {
            EmbeddingIndexRebuildResult step = await RunNextBatchAsync(maxChunks, cancellationToken)
                .ConfigureAwait(false);
            attempted += step.ChunksAttempted;
            embedded += step.ChunksEmbedded;
            failed += step.ChunksFailed;

            if (step.Completed || step.ProviderUnavailable || step.ProviderChanged || step.ChunksFailed > 0)
            {
                return step with
                {
                    ChunksAttempted = attempted,
                    ChunksEmbedded = embedded,
                    ChunksFailed = failed,
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingIndexRebuildResult> RunNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunks);

        EmbeddingIndexState state = await _lifecycle.GetStateAsync(cancellationToken)
            .ConfigureAwait(false);
        string stagingVersion = state.StagingIndexVersion ?? $"semantic-rebuild-{Guid.NewGuid():N}";
        if (state.StagingIndexVersion is null)
        {
            state = await _lifecycle.BeginRebuildAsync(stagingVersion, cancellationToken)
                .ConfigureAwait(false);
        }

        EmbeddingGenerationBatchResult batch = await _generation
            .GenerateNextBatchAsync(maxChunks, stagingVersion, cancellationToken)
            .ConfigureAwait(false);

        if (batch.ProviderUnavailable || batch.ProviderChanged || batch.ChunksFailed > 0 || batch.ChunksAttempted > 0)
        {
            return new EmbeddingIndexRebuildResult(
                Completed: false,
                state.ActiveIndexVersion,
                stagingVersion,
                batch.ChunksAttempted,
                batch.ChunksEmbedded,
                batch.ChunksFailed,
                batch.ProviderUnavailable,
                batch.ProviderChanged);
        }

        // Nothing left to embed: swap the active pointer in one transaction.
        EmbeddingIndexState promoted = await _lifecycle
            .PromoteAsync(stagingVersion, cancellationToken)
            .ConfigureAwait(false);
        return new EmbeddingIndexRebuildResult(
            Completed: true,
            promoted.ActiveIndexVersion,
            promoted.StagingIndexVersion,
            ChunksAttempted: 0,
            ChunksEmbedded: 0,
            ChunksFailed: 0,
            ProviderUnavailable: false);
    }
}
