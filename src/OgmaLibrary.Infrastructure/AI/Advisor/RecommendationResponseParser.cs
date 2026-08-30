using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Parses provider recommendation JSON into advisor domain cards.</summary>
public sealed class RecommendationResponseParser : IRecommendationResponseParser
{
    /// <inheritdoc />
    public IReadOnlyList<RecommendationCard> Parse(
        string responseText,
        string modelUsed,
        AiPrivacyTier tier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseText);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelUsed);

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseText);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, out JsonElement recommendations, "recommendations", "items"))
            {
                root = recommendations;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new AdvisorParseException("Recommendation response must be a JSON array.");
            }

            List<RecommendationCard> cards = [];
            foreach (JsonElement item in root.EnumerateArray())
            {
                cards.Add(ParseCard(item, modelUsed, tier, cards.Count + 1));
            }

            if (cards.Count == 0)
            {
                throw new AdvisorParseException("Recommendation response contained no cards.");
            }

            return cards;
        }
        catch (JsonException ex)
        {
            throw new AdvisorParseException($"Recommendation response was not valid JSON: {ex.Message}");
        }
    }

    private static RecommendationCard ParseCard(
        JsonElement item,
        string modelUsed,
        AiPrivacyTier tier,
        int fallbackRank)
    {
        string bookId = GetRequiredString(item, "book_id", "bookId", "BookId");
        int rank = GetOptionalInt(item, fallbackRank, "rank", "Rank");
        double confidence = GetRequiredDouble(item, "confidence", "Confidence");
        string summary = ReadSummary(item);
        IReadOnlyList<ProvenanceItem> provenance = ReadProvenance(item, bookId);

        RecommendationExplanation explanation = new(summary, provenance, modelUsed, tier);
        return new RecommendationCard(new BookId(bookId), rank, new ConfidenceScore(confidence), explanation);
    }

    private static string ReadSummary(JsonElement item)
    {
        if (!TryGetProperty(item, out JsonElement explanation, "explanation", "Explanation"))
        {
            return GetRequiredString(item, "summary", "Summary");
        }

        return explanation.ValueKind == JsonValueKind.String
            ? explanation.GetString() ?? string.Empty
            : GetRequiredString(explanation, "summary", "Summary");
    }

    private static List<ProvenanceItem> ReadProvenance(JsonElement item, string fallbackBookId)
    {
        if (!TryGetProperty(item, out JsonElement provenance, "provenance", "provenance_items", "provenanceItems", "Provenance"))
        {
            throw new AdvisorParseException("Recommendation card is missing provenance.");
        }

        if (provenance.ValueKind != JsonValueKind.Array)
        {
            throw new AdvisorParseException("Recommendation provenance must be an array.");
        }

        List<ProvenanceItem> items = [];
        foreach (JsonElement entry in provenance.EnumerateArray())
        {
            string bookId = GetOptionalString(entry, fallbackBookId, "book_id", "bookId", "BookId");
            string field = GetOptionalString(entry, "Description", "field", "match_field", "matchField", "MatchField");
            string fieldValue = GetOptionalString(entry, field, "field_value", "fieldValue", "value", "Value");
            string? source = GetOptionalNullableString(entry, "source", "source_label", "sourceLabel");
            string? version = GetOptionalNullableString(entry, "evidence_version", "evidenceVersion", "version");
            string? uncertainty = GetOptionalNullableString(entry, "uncertainty", "uncertainty_label", "uncertaintyLabel");
            items.Add(new ProvenanceItem(new BookId(bookId), ParseMatchField(field), fieldValue, source, version, uncertainty));
        }

        return items;
    }

    private static RecommendationMatchField ParseMatchField(string value)
    {
        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.ToUpperInvariant() switch
        {
            "TITLE" => RecommendationMatchField.Title,
            "AUTHOR" => RecommendationMatchField.Author,
            "TAG" or "TAGS" => RecommendationMatchField.Tags,
            "DESCRIPTION" or "SUMMARY" => RecommendationMatchField.Description,
            "SEMANTIC" or "SEMANTICSCORE" => RecommendationMatchField.SemanticScore,
            _ => RecommendationMatchField.Description,
        };
    }

    private static string GetRequiredString(JsonElement element, params string[] names)
    {
        string? value = GetOptionalString(element, null, names);
        return string.IsNullOrWhiteSpace(value)
            ? throw new AdvisorParseException($"Recommendation response is missing '{names[0]}'.")
            : value;
    }

    private static string GetOptionalString(JsonElement element, string? fallback, params string[] names)
    {
        if (!TryGetProperty(element, out JsonElement property, names))
        {
            return fallback ?? string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? fallback ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            _ => fallback ?? string.Empty,
        };
    }

    private static string? GetOptionalNullableString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out JsonElement property, names) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int GetOptionalInt(JsonElement element, int fallback, params string[] names) =>
        TryGetProperty(element, out JsonElement property, names) && property.TryGetInt32(out int value)
            ? value
            : fallback;

    private static double GetRequiredDouble(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out JsonElement property, names) || !property.TryGetDouble(out double value))
        {
            throw new AdvisorParseException($"Recommendation response is missing numeric '{names[0]}'.");
        }

        return value;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        property = default;
        return false;
    }
}
