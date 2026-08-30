using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Ensures recommendation provenance references only local candidates.</summary>
public sealed class RecommendationProvenanceValidator : IRecommendationProvenanceValidator
{
    private const string EvidenceVersion = "advisor-evidence-v1";

    /// <inheritdoc />
    public IReadOnlyList<RecommendationCard> Validate(
        IReadOnlyList<RecommendationCard> cards,
        IReadOnlyList<BookMetadataDto> localCandidates,
        int maxResults,
        string modelUsed,
        AiPrivacyTier tier)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(localCandidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelUsed);

        Dictionary<string, BookMetadataDto> local = localCandidates.ToDictionary(candidate => candidate.BookId, StringComparer.Ordinal);
        int checkedItems = 0;
        int invalidItems = 0;
        List<RecommendationCard> sanitized = [];

        foreach (RecommendationCard card in cards)
        {
            checkedItems++;
            if (!local.ContainsKey(card.BookId.Value))
            {
                invalidItems++;
                continue;
            }

            List<ProvenanceItem> provenance = [];
            foreach (ProvenanceItem item in card.Explanation.ProvenanceItems)
            {
                checkedItems++;
                if (local.TryGetValue(item.BookId.Value, out BookMetadataDto? candidate) && HasVerifiedField(candidate, item))
                {
                    provenance.Add(Stamp(item));
                }
                else
                {
                    invalidItems++;
                }
            }

            if (provenance.Count == 0)
            {
                provenance.Add(BuildFallbackProvenance(local[card.BookId.Value]));
            }

            sanitized.Add(Rebuild(card, sanitized.Count + 1, provenance));
        }

        if (checkedItems > 0 && invalidItems > checkedItems / 2)
        {
            return BuildFallback(localCandidates, maxResults, modelUsed, tier);
        }

        return sanitized.Take(maxResults).ToArray();
    }

    private static RecommendationCard Rebuild(RecommendationCard card, int rank, IReadOnlyList<ProvenanceItem> provenance)
    {
        RecommendationExplanation explanation = new(
            card.Explanation.Summary,
            provenance,
            card.Explanation.ModelUsed,
            card.Explanation.Tier);

        return new RecommendationCard(card.BookId, rank, card.Confidence, explanation);
    }

    private static RecommendationCard[] BuildFallback(
        IReadOnlyList<BookMetadataDto> candidates,
        int maxResults,
        string modelUsed,
        AiPrivacyTier tier) =>
        candidates
            .Take(maxResults)
            .Select((candidate, index) =>
            {
                ProvenanceItem provenance = BuildFallbackProvenance(candidate);
                RecommendationExplanation explanation = new(
                    $"Local catalogue fallback based on {provenance.MatchField}.",
                    [provenance],
                    modelUsed,
                    tier);
                return new RecommendationCard(new BookId(candidate.BookId), index + 1, new ConfidenceScore(0.5), explanation);
            })
            .ToArray();

    private static ProvenanceItem BuildFallbackProvenance(BookMetadataDto candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.Title))
        {
            return new ProvenanceItem(
                new BookId(candidate.BookId),
                RecommendationMatchField.Title,
                candidate.Title,
                "metadata.title",
                EvidenceVersion,
                "Provider explanation was not independently verified.");
        }

        string author = candidate.Authors.Count > 0 ? candidate.Authors[0] : "local catalogue";
        return new ProvenanceItem(
            new BookId(candidate.BookId),
            RecommendationMatchField.Author,
            author,
            "metadata.author",
            EvidenceVersion,
            "Provider explanation was not independently verified.");
    }

    private static ProvenanceItem Stamp(ProvenanceItem item) =>
        new(
            item.BookId,
            item.MatchField,
            item.FieldValue,
            item.SourceLabel ?? $"metadata.{item.MatchField.ToString().ToLowerInvariant()}",
            item.EvidenceVersion ?? EvidenceVersion,
            item.UncertaintyLabel);

    private static bool HasVerifiedField(BookMetadataDto candidate, ProvenanceItem item)
    {
        IReadOnlyList<string> values = item.MatchField switch
        {
            RecommendationMatchField.Title => candidate.Title is null ? [] : [candidate.Title],
            RecommendationMatchField.Author => candidate.Authors,
            RecommendationMatchField.Tags => candidate.Tags,
            RecommendationMatchField.Description => [candidate.Description ?? string.Empty, candidate.Notes ?? string.Empty],
            RecommendationMatchField.SemanticScore => [],
            _ => [],
        };

        return values.Any(value => !string.IsNullOrWhiteSpace(value) &&
            value.Contains(item.FieldValue, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Deterministic structural oracle for recommendation cards.</summary>
public sealed class RecommendationStructuralValidator : IRecommendationStructuralValidator
{
    /// <inheritdoc />
    public AdvisorValidationResult Validate(IReadOnlyList<RecommendationCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        List<string> errors = [];
        for (int i = 0; i < cards.Count; i++)
        {
            RecommendationCard card = cards[i];
            if (string.IsNullOrWhiteSpace(card.BookId.Value))
            {
                errors.Add($"Card {i} has no book id.");
            }

            if (card.Rank != i + 1)
            {
                errors.Add($"Card {card.BookId} rank is not sequential.");
            }

            if (card.Confidence.Value is < 0.0 or > 1.0 || double.IsNaN(card.Confidence.Value))
            {
                errors.Add($"Card {card.BookId} confidence is outside [0,1].");
            }

            if (string.IsNullOrWhiteSpace(card.Explanation.Summary))
            {
                errors.Add($"Card {card.BookId} explanation is empty.");
            }

            if (card.Explanation.ProvenanceItems.Count == 0)
            {
                errors.Add($"Card {card.BookId} has no provenance.");
            }
        }

        return errors.Count == 0 ? AdvisorValidationResult.Success : new AdvisorValidationResult(false, errors);
    }
}
