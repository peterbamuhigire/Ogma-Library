using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Implements the SRS §8.3 confidence merge formula (FR-META-003, DECISIONS.md D-007).
/// </summary>
/// <remarks>
/// <para>
/// Formula:
/// <code>
/// FieldConfidence(f, provider) =
///   ProviderWeight(provider)          // GoogleBooks=0.85, OpenLibrary=0.80
///   × MatchScore(f, existingValue)    // 0.0–1.0 string similarity
///   × RecencyBonus(lookupAge)         // 1.0 &lt;7d, 0.85 &lt;30d, 0.70 older
///
/// MergedConfidence(f) = max over all providers of FieldConfidence(f, p)
/// </code>
/// </para>
/// </remarks>
public sealed class ConfidenceMergeService : IConfidenceMergeService
{
    private static readonly IReadOnlyList<string> CoreFields =
    [
        "Title",
        "Author",
        "ISBN",
        "Year",
        "Publisher",
        "Description",
        "Categories",
    ];

    private static readonly Dictionary<string, double> ProviderWeights =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GoogleBooks"] = 0.85,
            ["OpenLibrary"] = 0.80,
            ["UserOverride"] = 1.00,
            ["PDF"] = 0.50,
        };

    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfidenceMergeService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context for reading existing field values.</param>
    public ConfidenceMergeService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MergedMetadataProposal>> MergeAsync(
        string bookId,
        IReadOnlyList<ProviderMetadataResult> lookupResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(lookupResults);

        // Load existing metadata fields for this book (for match scoring and override detection).
        var existingFields = await _context.BookMetadataFields
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingByName = existingFields.ToDictionary(
            f => f.FieldName,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var proposals = new List<MergedMetadataProposal>(CoreFields.Count);

        foreach (string field in CoreFields)
        {
            existingByName.TryGetValue(field, out var existingRow);

            // If the user has manually overridden this field, it is immutable.
            if (existingRow?.IsOverridden == true)
            {
                proposals.Add(new MergedMetadataProposal(
                    FieldName: field,
                    ProposedValue: existingRow.Value,
                    CurrentValue: existingRow.Value,
                    MergedConfidence: 1.0,
                    WinningProvider: "UserOverride",
                    Alternatives: []));
                continue;
            }

            string? existingValue = existingRow?.Value;
            var candidates = new List<(string Provider, string? Value, double Confidence)>();

            foreach (ProviderMetadataResult result in lookupResults)
            {
                if (result.Confidence <= 0.0)
                {
                    continue; // Provider returned a failure result
                }

                string? fieldValue = ExtractField(result, field);
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    continue;
                }

                double providerWeight = GetProviderWeight(result.Provider);
                double matchScore = ComputeMatchScore(field, fieldValue, existingValue);
                double recencyBonus = ComputeRecencyBonus(result.RetrievedUtc);
                double fieldConfidence = providerWeight * matchScore * recencyBonus;

                candidates.Add((result.Provider, fieldValue, fieldConfidence));
            }

            if (candidates.Count == 0)
            {
                // No provider has a value for this field; return current value as the proposal.
                proposals.Add(new MergedMetadataProposal(
                    FieldName: field,
                    ProposedValue: existingValue,
                    CurrentValue: existingValue,
                    MergedConfidence: existingRow?.Confidence ?? 0.0,
                    WinningProvider: existingRow?.Source ?? string.Empty,
                    Alternatives: []));
                continue;
            }

            // Select the candidate with the highest confidence.
            var sorted = candidates.OrderByDescending(c => c.Confidence).ToList();
            var winner = sorted[0];

            var alternatives = sorted.Skip(1)
                .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => new AlternativeFieldValue(c.Value!, c.Provider, c.Confidence))
                .ToList();

            proposals.Add(new MergedMetadataProposal(
                FieldName: field,
                ProposedValue: winner.Value,
                CurrentValue: existingValue,
                MergedConfidence: winner.Confidence,
                WinningProvider: winner.Provider,
                Alternatives: alternatives));
        }

        return proposals;
    }

    /// <summary>
    /// Returns the trust weight for a provider.
    /// Unknown providers default to 0.5.
    /// </summary>
    internal static double GetProviderWeight(string provider) =>
        ProviderWeights.TryGetValue(provider, out double w) ? w : 0.5;

    /// <summary>
    /// Computes the recency bonus based on how old the lookup result is.
    /// </summary>
    internal static double ComputeRecencyBonus(DateTimeOffset retrievedUtc)
    {
        TimeSpan age = DateTimeOffset.UtcNow - retrievedUtc;
        return age.TotalDays switch
        {
            < 7 => 1.0,
            < 30 => 0.85,
            _ => 0.70,
        };
    }

    /// <summary>
    /// Computes a string match score between a proposed field value and the existing
    /// catalogue value. When there is no existing value, returns 1.0 (no penalty).
    /// </summary>
    internal static double ComputeMatchScore(string fieldName, string? proposed, string? existing)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            // No existing value → new data, no penalty.
            return 1.0;
        }

        if (string.IsNullOrWhiteSpace(proposed))
        {
            return 0.0;
        }

        return fieldName switch
        {
            "ISBN" => string.Equals(proposed, existing, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0,
            "Year" => string.Equals(proposed, existing, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.5,
            "Title" or "Author" or "Publisher" => NormalizedLevenshteinSimilarity(proposed, existing),
            _ => SimpleTokenOverlap(proposed, existing),
        };
    }

    /// <summary>
    /// Normalized Levenshtein similarity in [0.0, 1.0] (1.0 = identical strings).
    /// </summary>
    internal static double NormalizedLevenshteinSimilarity(string a, string b)
    {
        if (a == b)
        {
            return 1.0;
        }

        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0)
        {
            return 1.0;
        }

        int distance = LevenshteinDistance(
            a.ToUpperInvariant(),
            b.ToUpperInvariant());

        return 1.0 - (distance / (double)maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (s.Length == 0)
        {
            return t.Length;
        }

        if (t.Length == 0)
        {
            return s.Length;
        }

        int[,] d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= t.Length; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }

    /// <summary>
    /// Simple token-overlap similarity for description/category fields.
    /// </summary>
    private static double SimpleTokenOverlap(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0.0;
        }

        int overlap = tokensA.Intersect(tokensB, StringComparer.OrdinalIgnoreCase).Count();
        return (double)overlap / Math.Max(tokensA.Count, tokensB.Count);
    }

    private static HashSet<string> Tokenize(string text) =>
        new(
            text.Split([' ', ',', '.', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2),
            StringComparer.OrdinalIgnoreCase);

    private static string? ExtractField(ProviderMetadataResult result, string fieldName) =>
        fieldName switch
        {
            "Title" => result.Title,
            "Author" => result.Authors.Count > 0 ? string.Join("; ", result.Authors) : null,
            "ISBN" => result.IsbnNormalized,
            "Year" => result.Year.HasValue ? result.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            "Publisher" => result.Publisher,
            "Description" => result.Description,
            "Categories" => result.Categories.Count > 0 ? string.Join(", ", result.Categories) : null,
            _ => null,
        };
}
