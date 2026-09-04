namespace OgmaLibrary.Application.Search;

/// <summary>
/// Semantic search contract over locally generated embeddings. Implementations
/// must fall back to exact Phase 10 search when embeddings or Ollama are absent.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Searches the local semantic index and returns book-level results.
    /// </summary>
    Task<SemanticSearchResponse> SearchAsync(
        string queryText,
        int maxResults,
        CancellationToken cancellationToken);
}

/// <summary>Semantic search response with degradation metadata.</summary>
public sealed record SemanticSearchResponse(
    bool ProviderUnavailable,
    bool UsedExactFallback,
    IReadOnlyList<SemanticSearchResult> Results,
    SemanticSearchAvailability Availability = SemanticSearchAvailability.Ready,
    bool EmbeddingCacheHit = false);

/// <summary>Explains whether semantic search is ready, degraded, or unavailable.</summary>
public enum SemanticSearchAvailability
{
    /// <summary>Semantic search produced a current result window.</summary>
    Ready = 0,

    /// <summary>The local semantic index has no eligible vectors yet.</summary>
    NoIndex = 1,

    /// <summary>The search path ran successfully but found no matches.</summary>
    NoMatches = 2,

    /// <summary>Exact search was used because semantic search was unavailable.</summary>
    Degraded = 3,
}

/// <summary>Book-level semantic search result.</summary>
public sealed record SemanticSearchResult(
    string BookId,
    string? Title,
    long? ChunkId,
    SearchChunkSource? Source,
    string? Snippet,
    float? SemanticScore,
    bool ExactFallback,
    double? HybridScore = null,
    IReadOnlyList<MatchLocation>? MatchLocations = null,
    ConfidenceLabel? ConfidenceLabel = null,
    int? PageIndex = null,
    SearchPageJumpTarget? PageJumpTarget = null);
