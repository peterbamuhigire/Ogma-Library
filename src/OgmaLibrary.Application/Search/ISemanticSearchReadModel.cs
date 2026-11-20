namespace OgmaLibrary.Application.Search;

/// <summary>
/// LAN-projection-ready read model for semantic index lifecycle events.
/// Phase 16 can subscribe to this stream without depending on desktop UI types.
/// </summary>
public interface ISemanticSearchReadModel
{
    /// <summary>Semantic indexing and erasure lifecycle events.</summary>
    IObservable<SemanticIndexEvent> Events { get; }

    /// <summary>
    /// Progress of the generation being built: the staging generation during a model change,
    /// otherwise the active one (Sept-23 Phase 14, slice 4).
    /// </summary>
    Task<SemanticIndexProgress> GetProgressAsync(CancellationToken cancellationToken);
}

/// <summary>Semantic indexing progress for the current provider and model.</summary>
/// <param name="ChunksEmbedded">Chunks with a vector from the current model in the target generation.</param>
/// <param name="ChunksTotal">Chunks eligible for embedding.</param>
/// <param name="BooksWaiting">Books with at least one chunk still to embed.</param>
/// <param name="EstimatedRemaining">Time left at the recent embedding rate, or null when no rate is known.</param>
/// <param name="IsRegenerating">True while a staged regeneration for a new model is in progress.</param>
public sealed record SemanticIndexProgress(
    int ChunksEmbedded,
    int ChunksTotal,
    int BooksWaiting,
    TimeSpan? EstimatedRemaining,
    bool IsRegenerating);

/// <summary>Semantic search index lifecycle event.</summary>
public abstract record SemanticIndexEvent
{
    /// <summary>A chunk embedding was generated successfully.</summary>
    public sealed record EmbeddingGenerated(
        long ChunkId,
        string BookId,
        int TotalEmbedded,
        int TotalChunks) : SemanticIndexEvent;

    /// <summary>The local Ollama service was not reachable.</summary>
    public sealed record OllamaUnavailable(DateTimeOffset DetectedAtUtc) : SemanticIndexEvent;

    /// <summary>A chunk embedding attempt failed but sibling chunks can continue.</summary>
    public sealed record EmbeddingFailed(
        long ChunkId,
        string BookId,
        string Reason) : SemanticIndexEvent;
}
