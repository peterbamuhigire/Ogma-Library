namespace OgmaLibrary.Application.Search;

/// <summary>
/// Searches catalogue metadata while the user types (FR-SEARCH-001). The
/// contract is Application-layer only so UI and LAN clients can consume search
/// results without referencing EF Core.
/// </summary>
public interface IMetadataSearchService
{
    /// <summary>
    /// Searches book metadata and returns relevance-ranked results.
    /// </summary>
    /// <param name="query">The user query text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>At most 50 relevance-ranked search results.</returns>
    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string? query,
        CancellationToken cancellationToken);
}

/// <summary>
/// One metadata search result for the global search UI.
/// </summary>
/// <param name="BookId">The stable catalogue book identity.</param>
/// <param name="Title">The bibliographic title, or a fallback title.</param>
/// <param name="Author">The first display author, or null.</param>
/// <param name="Score">Deterministic relevance score.</param>
/// <param name="MatchedFields">Fields that contributed to the score.</param>
/// <param name="CorrectionSuggestion">The local value that matched a fuzzy query, if any.</param>
public sealed record MetadataSearchResult(
    string BookId,
    string? Title,
    string? Author,
    int Score,
    IReadOnlyList<string> MatchedFields,
    string? CorrectionSuggestion = null);
