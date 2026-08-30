using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Builds grounded local recommendation cards without a provider.</summary>
public sealed class DeterministicAdvisorFallback
{
    private const string Model = "local-advisor-v1";

    /// <summary>Returns only locally supplied, constraint-filtered recommendation cards.</summary>
    public static IReadOnlyList<RecommendationCard> Build(
        IReadOnlyList<BookMetadataDto> candidates,
        AdvisorIntent intent,
        int maxResults,
        AiPrivacyTier tier)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(intent);
        if (maxResults is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "Advisor fallback result count must be between 1 and 25.");
        }

        IReadOnlyList<BookMetadataDto> ranked = AdvisorCandidateRanker.Rank(candidates, intent, Math.Min(50, maxResults));
        return ranked
            .Select((candidate, index) => BuildCard(candidate, intent, index + 1, tier))
            .ToArray();
    }

    private static RecommendationCard BuildCard(
        BookMetadataDto candidate,
        AdvisorIntent intent,
        int rank,
        AiPrivacyTier tier)
    {
        IReadOnlyList<RecommendationMatchField> fields = AdvisorCandidateRanker.MatchedFields(candidate, intent);
        List<ProvenanceItem> provenance = fields
            .Select(field => new ProvenanceItem(
                new BookId(candidate.BookId),
                field,
                FieldValue(candidate, field),
                $"metadata.{field.ToString().ToLowerInvariant()}",
                "advisor-evidence-v1"))
            .ToList();
        if (provenance.Count == 0)
        {
            provenance.Add(new ProvenanceItem(
                new BookId(candidate.BookId),
                RecommendationMatchField.Title,
                candidate.Title ?? candidate.BookId,
                "metadata.title",
                "advisor-evidence-v1"));
        }

        double score = AdvisorCandidateRanker.Score(candidate, intent);
        double confidence = Math.Clamp(0.45 + (score * 0.45), 0.45, 0.90);
        string summary = intent.PositiveTerms.Count == 0
            ? "Selected from the local catalogue using the requested constraints."
            : $"Matches local catalogue evidence for {string.Join(", ", intent.PositiveTerms.Take(3))}.";
        RecommendationExplanation explanation = new(summary, provenance, Model, tier);
        return new RecommendationCard(
            new BookId(candidate.BookId),
            rank,
            new ConfidenceScore(confidence),
            explanation);
    }

    private static string FieldValue(BookMetadataDto candidate, RecommendationMatchField field) => field switch
    {
        RecommendationMatchField.Title => candidate.Title ?? candidate.BookId,
        RecommendationMatchField.Author => candidate.Authors.Count > 0 ? candidate.Authors[0] : candidate.BookId,
        RecommendationMatchField.Tags => candidate.Tags.Count > 0 ? candidate.Tags[0] : candidate.BookId,
        RecommendationMatchField.Description => candidate.Description ?? candidate.Notes ?? candidate.BookId,
        RecommendationMatchField.SemanticScore => "local deterministic score",
        _ => candidate.BookId,
    };
}
