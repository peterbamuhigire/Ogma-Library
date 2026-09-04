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
}

/// <summary>Outcome of one bounded semantic-index rebuild attempt.</summary>
public sealed record EmbeddingIndexRebuildResult(
    bool Completed,
    string ActiveIndexVersion,
    string? StagingIndexVersion,
    int ChunksAttempted,
    int ChunksEmbedded,
    int ChunksFailed,
    bool ProviderUnavailable);
