namespace OgmaLibrary.Application.Search;

/// <summary>Runs a resumable, side-by-side semantic-index rebuild.</summary>
public interface IEmbeddingIndexRebuildService
{
    /// <summary>
    /// Builds or resumes the durable staging generation and promotes it only
    /// after every chunk has a valid vector.
    /// </summary>
    Task<EmbeddingIndexRebuildResult> RebuildAsync(
        int maxChunks,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs one bounded batch of the staging generation, beginning it when none exists, and
    /// promotes it atomically once a batch finds nothing left to embed. Background workers
    /// call this repeatedly so a regeneration interleaves with pause, reader yield and
    /// cancellation (Sept-23 Phase 14, slice 4).
    /// </summary>
    Task<EmbeddingIndexRebuildResult> RunNextBatchAsync(
        int maxChunks,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of one bounded semantic-index rebuild attempt.</summary>
public sealed record EmbeddingIndexRebuildResult(
    bool Completed,
    string ActiveIndexVersion,
    string? StagingIndexVersion,
    int ChunksAttempted,
    int ChunksEmbedded,
    int ChunksFailed,
    bool ProviderUnavailable,
    bool ProviderChanged = false);
