namespace OgmaLibrary.Application.Search;

/// <summary>Bounded, page-oriented catalogue search request.</summary>
public sealed record CatalogueSearchQuery(
    string? Text,
    string? Field = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>One explainable facet count for a returned search page.</summary>
public sealed record CatalogueSearchFacet(string Field, int Count);

/// <summary>One page-oriented catalogue search result.</summary>
public sealed record CatalogueSearchItem(
    string BookId,
    string? Title,
    string? Author,
    int Score,
    IReadOnlyList<string> MatchedFields,
    SearchSnippet? HighlightedTitle = null,
    SearchSnippet? HighlightedAuthor = null,
    string? CorrectionSuggestion = null,
    IReadOnlyList<FtsSearchResult>? FullTextHits = null);

/// <summary>Stable page result with bounded facet and fallback metadata.</summary>
public sealed record CatalogueSearchPage(
    IReadOnlyList<CatalogueSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<CatalogueSearchFacet> Facets,
    bool UsedFullTextFallback,
    string? Notice = null);

/// <summary>
/// Searches catalogue metadata with stable paging and uses full text when the
/// metadata path has no usable result.
/// </summary>
public interface ICatalogueSearchService
{
    /// <summary>Executes one bounded, deterministic catalogue search.</summary>
    Task<CatalogueSearchPage> SearchAsync(
        CatalogueSearchQuery query,
        CancellationToken cancellationToken = default);
}
