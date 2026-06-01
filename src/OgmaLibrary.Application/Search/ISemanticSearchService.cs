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
    IReadOnlyList<SemanticSearchResult> Results);

/// <summary>Book-level semantic search result.</summary>
public sealed record SemanticSearchResult(
    string BookId,
    string? Title,
    long? ChunkId,
    SearchChunkSource? Source,
    string? Snippet,
    float? SemanticScore,
    bool ExactFallback);
