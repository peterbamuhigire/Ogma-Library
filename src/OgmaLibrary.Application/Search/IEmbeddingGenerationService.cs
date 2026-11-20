namespace OgmaLibrary.Application.Search;

/// <summary>
/// Generates local semantic embeddings for search chunks. Implementations must
/// be idempotent and degrade gracefully when the local embedding provider is
/// unavailable.
/// </summary>
public interface IEmbeddingGenerationService
{
    /// <summary>
    /// Embeds the next batch of chunks that do not have a current vector.
    /// </summary>
    Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of one embedding-generation polling batch. <c>ProviderChanged</c> is true when the
/// provider or model changed during the batch; the batch stopped without writing a vector for
/// the new model so a generation never mixes models (Sept-23 Phase 14, slice 4).
/// </summary>
public sealed record EmbeddingGenerationBatchResult(
    int ChunksAttempted,
    int ChunksEmbedded,
    int ChunksFailed,
    int ChunksSkipped,
    bool ProviderUnavailable,
    int InputTokenCount = 0,
    long ExternalProviderEgressBytes = 0,
    decimal EstimatedExternalProviderCostUsd = 0m,
    bool ProviderChanged = false);
