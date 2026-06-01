namespace OgmaLibrary.Application.Search;

/// <summary>
/// Produces search-result explanation metadata from exact, FTS, semantic, and
/// hybrid ranking signals.
/// </summary>
public interface IMatchLocationService
{
    /// <summary>Returns ordered, distinct match locations for one result.</summary>
    IReadOnlyList<MatchLocation> GetLocations(
        CombinedSearchResult? exactResult,
        SemanticSearchResult? semanticResult,
        float semanticThreshold = MatchLocationService.DefaultSemanticThreshold);

    /// <summary>Builds the Phase 11 enriched result metadata for UI badges.</summary>
    SearchResultEnrichment Enrich(
        HybridRankedResult result,
        float semanticThreshold = MatchLocationService.DefaultSemanticThreshold);
}

/// <summary>Enriched search result metadata for match badges and confidence labels.</summary>
public sealed record SearchResultEnrichment(
    string BookId,
    IReadOnlyList<MatchLocation> MatchLocations,
    ConfidenceLabel ConfidenceLabel,
    double HybridScore,
    double? SemanticScore);

