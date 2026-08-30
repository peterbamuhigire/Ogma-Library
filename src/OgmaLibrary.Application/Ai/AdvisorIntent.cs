using System.Text.RegularExpressions;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Length preference inferred from an advisor request.</summary>
public enum AdvisorLengthPreference
{
    /// <summary>No length constraint was expressed.</summary>
    Any = 0,

    /// <summary>The reader asked for a short or quick book.</summary>
    ShortBook = 1,

    /// <summary>The reader asked for a long or comprehensive book.</summary>
    LongBook = 2,
}

/// <summary>Deterministically extracted, editable constraints for one advisor request.</summary>
public sealed record AdvisorIntent
{
    /// <summary>Stable schema version for the extracted intent.</summary>
    public const string SchemaVersion = "advisor-intent-v1";

    /// <summary>Creates an advisor intent.</summary>
    public AdvisorIntent(
        IReadOnlyList<string>? positiveTerms = null,
        IReadOnlyList<string>? negativeTerms = null,
        IReadOnlyList<string>? moodTerms = null,
        DifficultyLabel? difficulty = null,
        AdvisorLengthPreference length = AdvisorLengthPreference.Any,
        string? comparisonReference = null,
        bool isBroadDiscovery = false)
    {
        PositiveTerms = NormalizeTerms(positiveTerms);
        NegativeTerms = NormalizeTerms(negativeTerms);
        MoodTerms = NormalizeTerms(moodTerms);
        Difficulty = difficulty;
        Length = length;
        ComparisonReference = string.IsNullOrWhiteSpace(comparisonReference)
            ? null
            : comparisonReference.Trim();
        IsBroadDiscovery = isBroadDiscovery;
    }

    /// <summary>The intent schema version.</summary>
    public string Version { get; } = AdvisorIntent.SchemaVersion;

    /// <summary>Positive topics, attributes, or concepts from the request.</summary>
    public IReadOnlyList<string> PositiveTerms { get; }

    /// <summary>Terms the result must avoid.</summary>
    public IReadOnlyList<string> NegativeTerms { get; }

    /// <summary>Positive tone or mood terms expressed by the reader.</summary>
    public IReadOnlyList<string> MoodTerms { get; }

    /// <summary>Requested difficulty band, when one can be inferred.</summary>
    public DifficultyLabel? Difficulty { get; }

    /// <summary>Requested book length.</summary>
    public AdvisorLengthPreference Length { get; }

    /// <summary>Reference title or work used by a comparison request.</summary>
    public string? ComparisonReference { get; }

    /// <summary>Whether the request asks for broad or surprising discovery.</summary>
    public bool IsBroadDiscovery { get; }

    private static string[] NormalizeTerms(IReadOnlyList<string>? terms) =>
        (terms ?? [])
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(term => term, StringComparer.Ordinal)
            .ToArray();
}

/// <summary>Deterministic parser for the eight supported advisor intent categories.</summary>
public static partial class AdvisorIntentParser
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "anything", "book", "books", "but", "by", "can", "do", "for", "from",
        "i", "in", "me", "of", "on", "or", "something", "that", "the", "this", "to", "with", "would",
        "rather", "than", "focused", "focus", "explaining", "explain", "teach", "teaching", "like", "similar",
        "please", "recommend", "recommendation", "want", "read", "reading", "choose", "surprise", "surprising",
    };

    private static readonly HashSet<string> MoodVocabulary = new(StringComparer.Ordinal)
    {
        "adventurous", "calm", "challenging", "dark", "depressing", "energising", "energetic", "funny", "hopeful",
        "humorous", "inspiring", "light", "optimistic", "practical", "reflective", "serious", "thoughtful", "uplifting",
    };

    private static readonly HashSet<string> DifficultyVocabulary = new(StringComparer.Ordinal)
    {
        "accessible", "advanced", "beginner", "beginners", "expert", "foundational", "introductory", "intermediate", "specialist",
    };

    private static readonly HashSet<string> LengthVocabulary = new(StringComparer.Ordinal)
    {
        "brief", "comprehensive", "deep", "long", "quick", "short", "thorough", "weekend",
    };

    /// <summary>Parses a user request without an external model or network call.</summary>
    /// <param name="queryText">Natural-language advisor request.</param>
    public static AdvisorIntent Parse(string queryText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);

        string normalized = queryText.Trim();
        string[] tokens = Tokenize(normalized);
        HashSet<string> negative = ExtractNegativeTerms(normalized, tokens);
        HashSet<string> moods = tokens.Where(MoodVocabulary.Contains).ToHashSet(StringComparer.Ordinal);
        DifficultyLabel? difficulty = ParseDifficulty(normalized, tokens);
        AdvisorLengthPreference length = ParseLength(tokens);
        string? comparisonReference = ExtractComparisonReference(normalized);
        bool broad = ContainsAny(tokens, "surprise", "unusual", "unexpected", "outside", "comfort");

        HashSet<string> positive = tokens
            .Where(token => !StopWords.Contains(token))
            .Where(token => !MoodVocabulary.Contains(token))
            .Where(token => !DifficultyVocabulary.Contains(token))
            .Where(token => !LengthVocabulary.Contains(token))
            .Where(token => !negative.Contains(token))
            .Where(token => token is not ("not" or "without" or "avoid" or "avoiding" or "except" or "excluding" or "less"))
            .ToHashSet(StringComparer.Ordinal);

        return new AdvisorIntent(
            positive.ToArray(),
            negative.ToArray(),
            moods.Where(mood => mood is not ("depressing" or "dark")).ToArray(),
            difficulty,
            length,
            comparisonReference,
            broad);
    }

    private static string[] Tokenize(string value) =>
        TokenRegex().Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .ToArray();

    private static HashSet<string> ExtractNegativeTerms(string query, string[] tokens)
    {
        HashSet<string> negative = new(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Length; i++)
        {
            bool ratherThan = tokens[i] == "rather" && i + 1 < tokens.Length && tokens[i + 1] == "than";
            bool marker = tokens[i] is "not" or "without" or "avoid" or "avoiding" or "except" or "excluding" or "minus" or "less" || ratherThan;
            bool withoutAssuming = tokens[i] == "without" && i + 1 < tokens.Length && tokens[i + 1] == "assuming";
            if (!marker && !withoutAssuming)
            {
                continue;
            }

            if (tokens[i] == "without")
            {
                if (withoutAssuming)
                {
                    continue;
                }

            }

            int start = ratherThan ? i + 2 : i + 1;
            for (int j = start; j < tokens.Length && j <= i + 6; j++)
            {
                if (tokens[j] is "but" or "and" or "rather" or "than" or "focused" or "focus")
                {
                    break;
                }

                if (!StopWords.Contains(tokens[j]) && !DifficultyVocabulary.Contains(tokens[j]) && !LengthVocabulary.Contains(tokens[j]))
                {
                    negative.Add(tokens[j]);
                }
            }
        }

        // "without assuming I studied economics" expresses introductory difficulty,
        // not an instruction to exclude economics.
        if (query.Contains("without assuming", StringComparison.OrdinalIgnoreCase))
        {
            negative.RemoveWhere(term => term is "studied" or "study" or "background" or "economics");
        }

        return negative;
    }

    private static DifficultyLabel? ParseDifficulty(string query, string[] tokens)
    {
        if (query.Contains("without assuming", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(tokens, "beginner", "beginners", "introductory", "accessible"))
        {
            return DifficultyLabel.Introductory;
        }

        if (tokens.Contains("foundational", StringComparer.Ordinal))
        {
            return DifficultyLabel.Foundational;
        }

        if (tokens.Contains("advanced", StringComparer.Ordinal))
        {
            return DifficultyLabel.Advanced;
        }

        if (tokens.Contains("expert", StringComparer.Ordinal) || tokens.Contains("specialist", StringComparer.Ordinal))
        {
            return DifficultyLabel.Expert;
        }

        return tokens.Contains("intermediate", StringComparer.Ordinal) ? DifficultyLabel.Intermediate : null;
    }

    private static AdvisorLengthPreference ParseLength(string[] tokens)
    {
        if (ContainsAny(tokens, "short", "quick", "brief", "weekend"))
        {
            return AdvisorLengthPreference.ShortBook;
        }

        return ContainsAny(tokens, "long", "deep", "thorough", "comprehensive")
            ? AdvisorLengthPreference.LongBook
            : AdvisorLengthPreference.Any;
    }

    private static string? ExtractComparisonReference(string query)
    {
        Match match = ComparisonRegex().Match(query);
        if (!match.Success)
        {
            return null;
        }

        string reference = match.Groups[1].Value.Trim();
        int boundary = reference.IndexOf(" but ", StringComparison.OrdinalIgnoreCase);
        if (boundary >= 0)
        {
            reference = reference[..boundary];
        }

        reference = reference.Trim().TrimEnd('.', ';', '?', '!');

        return string.IsNullOrWhiteSpace(reference) ? null : reference;
    }

    private static bool ContainsAny(string[] tokens, params string[] values) =>
        values.Any(tokens.Contains);

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex("\\blike\\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComparisonRegex();
}
