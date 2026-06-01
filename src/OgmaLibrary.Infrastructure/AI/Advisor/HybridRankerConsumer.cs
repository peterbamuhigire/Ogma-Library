using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Advisor adapter over Phase 11 semantic/hybrid search results.</summary>
public sealed class HybridRankerConsumer : IHybridRankerConsumer
{
    private const int CandidateLimit = 50;

    private readonly ISemanticSearchService _semanticSearch;

    /// <summary>Initializes a new instance of <see cref="HybridRankerConsumer"/>.</summary>
    public HybridRankerConsumer(ISemanticSearchService semanticSearch)
    {
        ArgumentNullException.ThrowIfNull(semanticSearch);
        _semanticSearch = semanticSearch;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RankedCandidate>> RankAsync(
        RecommendationQuery query,
        IReadOnlyList<BookMetadataDto> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return [];
        }

        SemanticSearchResponse response = await _semanticSearch
            .SearchAsync(query.QueryText, Math.Min(CandidateLimit, Math.Max(query.MaxResults, candidates.Count)), cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, SemanticSearchResult> byBook = response.Results
            .GroupBy(result => result.BookId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(result => result.HybridScore ?? NormalizeSemantic(result.SemanticScore) ?? 0.0)
                    .First(),
                StringComparer.Ordinal);

        return candidates
            .Select(candidate => ToRankedCandidate(candidate, byBook.GetValueOrDefault(candidate.BookId)))
            .OrderByDescending(candidate => candidate.HybridScore)
            .ThenBy(candidate => candidate.Candidate.BookId, StringComparer.Ordinal)
            .ToArray();
    }

    private static RankedCandidate ToRankedCandidate(BookMetadataDto candidate, SemanticSearchResult? result)
    {
        double? semantic = NormalizeSemantic(result?.SemanticScore);
        double hybrid = Math.Clamp(result?.HybridScore ?? semantic ?? 0.0, 0.0, 1.0);
        return new RankedCandidate(candidate, hybrid, semantic);
    }

    private static double? NormalizeSemantic(float? score)
    {
        if (!score.HasValue)
        {
            return null;
        }

        double clamped = Math.Clamp(score.Value, -1.0f, 1.0f);
        return (clamped + 1.0) / 2.0;
    }
}
