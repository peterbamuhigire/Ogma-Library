using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Merges provider recommendation order with Phase 11 ranking signals.</summary>
public sealed class HybridRecommendationMerger : IHybridRecommendationMerger
{
    /// <inheritdoc />
    public IReadOnlyList<RecommendationCard> Merge(
        IReadOnlyList<RecommendationCard> aiCards,
        IReadOnlyList<RankedCandidate> rankedCandidates,
        AdvisorOptions options,
        int maxResults)
    {
        ArgumentNullException.ThrowIfNull(aiCards);
        ArgumentNullException.ThrowIfNull(rankedCandidates);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        Dictionary<string, RankedCandidate> rankByBook = rankedCandidates
            .ToDictionary(candidate => candidate.Candidate.BookId, StringComparer.Ordinal);
        double totalWeight = options.AiWeight + options.SemanticWeight;
        if (totalWeight <= 0.0)
        {
            totalWeight = 1.0;
        }

        return aiCards
            .Select(card => MergeScore(card, rankByBook.GetValueOrDefault(card.BookId.Value), aiCards.Count, options, totalWeight))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Card.BookId.Value, StringComparer.Ordinal)
            .Take(maxResults)
            .Select((item, index) => Rebuild(item.Card, index + 1, item.Score, item.RankedCandidate))
            .ToArray();
    }

    private static MergedRecommendation MergeScore(
        RecommendationCard card,
        RankedCandidate? rankedCandidate,
        int cardCount,
        AdvisorOptions options,
        double totalWeight)
    {
        double aiScore = cardCount <= 1
            ? 1.0
            : 1.0 - ((card.Rank - 1.0) / cardCount);
        double semanticScore = rankedCandidate?.HybridScore ?? 0.0;
        double score = ((aiScore * options.AiWeight) + (semanticScore * options.SemanticWeight)) / totalWeight;
        return new MergedRecommendation(card, Math.Clamp(score, 0.0, 1.0), rankedCandidate);
    }

    private static RecommendationCard Rebuild(
        RecommendationCard card,
        int rank,
        double score,
        RankedCandidate? rankedCandidate)
    {
        IReadOnlyList<ProvenanceItem> provenance = rankedCandidate is null
            ? card.Explanation.ProvenanceItems
            : AddSemanticProvenance(card.Explanation.ProvenanceItems, rankedCandidate);
        RecommendationExplanation explanation = new(
            card.Explanation.Summary,
            provenance,
            card.Explanation.ModelUsed,
            card.Explanation.Tier);

        return new RecommendationCard(card.BookId, rank, new ConfidenceScore(score), explanation);
    }

    private static IReadOnlyList<ProvenanceItem> AddSemanticProvenance(
        IReadOnlyList<ProvenanceItem> existing,
        RankedCandidate rankedCandidate)
    {
        if (rankedCandidate.SemanticScore is null ||
            existing.Any(item =>
                item.MatchField == RecommendationMatchField.SemanticScore &&
                string.Equals(item.BookId.Value, rankedCandidate.Candidate.BookId, StringComparison.Ordinal)))
        {
            return existing;
        }

        List<ProvenanceItem> merged = [.. existing];
        merged.Add(new ProvenanceItem(
            new BookId(rankedCandidate.Candidate.BookId),
            RecommendationMatchField.SemanticScore,
            rankedCandidate.SemanticScore.Value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "semantic-ranking",
            "advisor-evidence-v1",
            "Ranking score is not a factual book claim."));
        return merged;
    }

    private sealed record MergedRecommendation(
        RecommendationCard Card,
        double Score,
        RankedCandidate? RankedCandidate);
}
