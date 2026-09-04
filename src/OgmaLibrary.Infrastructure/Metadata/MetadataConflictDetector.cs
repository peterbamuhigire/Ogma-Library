using System.Globalization;
using System.Text.RegularExpressions;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Detects provider disagreement using field-aware normalization. Empty and
/// zero-confidence failure placeholders are ignored. The original candidate values
/// are retained for a review consumer, while audit persistence stores only field and
/// provider provenance.
/// </summary>
public sealed class MetadataConflictDetector : IMetadataConflictDetector
{
    private static readonly string[] Fields =
    [
        "Title",
        "Author",
        "ISBN",
        "Year",
        "Publisher",
        "Description",
        "Categories",
        "CoverUrl",
        "PageCount",
        "Language",
    ];

    /// <inheritdoc />
    public MetadataConflictReport Detect(IReadOnlyList<ProviderMetadataResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var conflicts = new List<MetadataFieldConflict>();
        foreach (string field in Fields)
        {
            var candidates = results
                .Where(result => result.Confidence > 0.0)
                .Select(result => (Result: result, Value: ExtractField(result, field)))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
                .Select(candidate => new
                {
                    candidate.Result.Provider,
                    Value = candidate.Value!,
                    Normalized = Normalize(field, candidate.Value!),
                    candidate.Result.Confidence,
                })
                .OrderBy(candidate => candidate.Provider, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Value, StringComparer.Ordinal)
                .ToList();

            if (candidates.Select(candidate => candidate.Normalized)
                .Distinct(StringComparer.Ordinal)
                .Count() <= 1)
            {
                continue;
            }

            conflicts.Add(new MetadataFieldConflict(
                field,
                candidates
                    .Select(candidate => new MetadataConflictCandidate(
                        candidate.Provider,
                        candidate.Value,
                        Math.Clamp(candidate.Confidence, 0.0, 1.0)))
                    .ToArray()));
        }

        return new MetadataConflictReport(conflicts);
    }

    private static string? ExtractField(ProviderMetadataResult result, string fieldName) =>
        fieldName switch
        {
            "Title" => result.Title,
            "Author" => result.Authors.Count == 0 ? null : string.Join("; ", result.Authors),
            "ISBN" => result.IsbnNormalized,
            "Year" => result.Year?.ToString(CultureInfo.InvariantCulture),
            "Publisher" => result.Publisher,
            "Description" => result.Description,
            "Categories" => result.Categories.Count == 0 ? null : string.Join(", ", result.Categories),
            "CoverUrl" => result.CoverUrl,
            "PageCount" => result.PageCount?.ToString(CultureInfo.InvariantCulture),
            "Language" => result.Language,
            _ => null,
        };

    private static string Normalize(string fieldName, string value)
    {
        string normalized = Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
        return fieldName switch
        {
            "ISBN" => new string(normalized.Where(char.IsLetterOrDigit).ToArray()),
            "Author" => string.Join(
                ";",
                normalized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Order(StringComparer.Ordinal)),
            "Categories" => string.Join(
                ",",
                normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Order(StringComparer.Ordinal)),
            "CoverUrl" => normalized.TrimEnd('/'),
            _ => normalized,
        };
    }
}
