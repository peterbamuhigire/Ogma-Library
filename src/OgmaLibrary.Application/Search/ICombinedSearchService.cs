namespace OgmaLibrary.Application.Search;

/// <summary>
/// Combines metadata and FTS5 hits into a deduplicated book-level result set.
/// </summary>
public interface ICombinedSearchService
{
    /// <summary>
    /// Searches metadata and indexed text, deduplicated by book id.
    /// </summary>
    Task<IReadOnlyList<CombinedSearchResult>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>One deduplicated search result spanning metadata and full text.</summary>
public sealed record CombinedSearchResult(
    string BookId,
    string? Title,
    string? Author,
    double Score,
    IReadOnlyList<string> MatchedFields,
    IReadOnlyList<FtsSearchResult> FtsHits,
    string FusionVersion = SearchContractVersions.CombinedFusion);
