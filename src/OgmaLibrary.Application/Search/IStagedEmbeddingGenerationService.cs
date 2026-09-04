namespace OgmaLibrary.Application.Search;

/// <summary>Embedding generation contract that targets an explicit index generation.</summary>
public interface IStagedEmbeddingGenerationService
{
    /// <summary>
    /// Embeds the next batch into <paramref name="indexVersion"/> without
    /// replacing the active semantic index.
    /// </summary>
    Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
        int maxChunks,
        string indexVersion,
        CancellationToken cancellationToken);
}
