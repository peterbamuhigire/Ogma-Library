namespace OgmaLibrary.Application.Search;

/// <summary>
/// LAN-projection-ready read model for semantic index lifecycle events.
/// Phase 16 can subscribe to this stream without depending on desktop UI types.
/// </summary>
public interface ISemanticSearchReadModel
{
    /// <summary>Semantic indexing and erasure lifecycle events.</summary>
    IObservable<SemanticIndexEvent> Events { get; }
}

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
