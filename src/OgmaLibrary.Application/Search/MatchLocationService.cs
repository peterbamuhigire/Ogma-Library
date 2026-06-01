namespace OgmaLibrary.Application.Search;

/// <summary>
/// Derives deterministic match-location badges and confidence labels for search
/// results.
/// </summary>
public sealed class MatchLocationService : IMatchLocationService
{
    /// <summary>Default raw cosine threshold for showing a semantic match badge.</summary>
    public const float DefaultSemanticThreshold = 0.30f;

    /// <inheritdoc />
    public IReadOnlyList<MatchLocation> GetLocations(
        CombinedSearchResult? exactResult,
        SemanticSearchResult? semanticResult,
        float semanticThreshold = DefaultSemanticThreshold)
    {
        var locations = new SortedSet<MatchLocation>();
        if (exactResult is not null)
        {
            AddMetadataLocations(exactResult.MatchedFields, locations);
            foreach (FtsSearchResult hit in exactResult.FtsHits)
            {
                locations.Add(MapSource(hit.Source));
            }
        }

        if (semanticResult?.SemanticScore >= semanticThreshold && !semanticResult.ExactFallback)
        {
            locations.Add(MatchLocation.Semantic);
        }

        return locations.ToList();
    }

    /// <inheritdoc />
    public SearchResultEnrichment Enrich(
        HybridRankedResult result,
        float semanticThreshold = DefaultSemanticThreshold)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SearchResultEnrichment(
            result.BookId,
            GetLocations(result.ExactResult, result.SemanticResult, semanticThreshold),
            LabelForScore(result.HybridScore),
            result.HybridScore,
            result.SemanticScore);
    }

    /// <summary>Converts a hybrid score into the Phase 11 confidence label.</summary>
    public static ConfidenceLabel LabelForScore(double hybridScore)
    {
        if (hybridScore >= 0.8)
        {
            return ConfidenceLabel.High;
        }

        if (hybridScore >= 0.5)
        {
            return ConfidenceLabel.Medium;
        }

        return ConfidenceLabel.Low;
    }

    private static void AddMetadataLocations(
        IEnumerable<string> matchedFields,
        SortedSet<MatchLocation> locations)
    {
        foreach (string matchedField in matchedFields)
        {
            string field = matchedField.Split(':', 2)[0];
            if (string.Equals(field, "full-text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryMapMetadataField(field, out MatchLocation location))
            {
                locations.Add(location);
            }
        }
    }

    private static bool TryMapMetadataField(string field, out MatchLocation location)
    {
        if (string.Equals(field, "title", StringComparison.OrdinalIgnoreCase))
        {
            location = MatchLocation.Title;
            return true;
        }

        if (string.Equals(field, "author", StringComparison.OrdinalIgnoreCase))
        {
            location = MatchLocation.Author;
            return true;
        }

        if (string.Equals(field, "tag", StringComparison.OrdinalIgnoreCase))
        {
            location = MatchLocation.Tag;
            return true;
        }

        if (string.Equals(field, "description", StringComparison.OrdinalIgnoreCase))
        {
            location = MatchLocation.Description;
            return true;
        }

        location = default;
        return false;
    }

    private static MatchLocation MapSource(SearchChunkSource source) =>
        source switch
        {
            SearchChunkSource.Page => MatchLocation.TextPage,
            SearchChunkSource.Note => MatchLocation.NotePage,
            SearchChunkSource.Tag => MatchLocation.Tag,
            SearchChunkSource.Description => MatchLocation.Description,
            SearchChunkSource.Toc => MatchLocation.Toc,
            _ => MatchLocation.TextPage,
        };
}
