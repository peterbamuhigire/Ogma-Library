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

/// <summary>Result of one embedding-generation polling batch.</summary>
public sealed record EmbeddingGenerationBatchResult(
    int ChunksAttempted,
    int ChunksEmbedded,
    int ChunksFailed,
    int ChunksSkipped,
    bool ProviderUnavailable);
