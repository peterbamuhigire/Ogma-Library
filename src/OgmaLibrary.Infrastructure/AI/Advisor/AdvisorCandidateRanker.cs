using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Deterministic, catalogue-bounded advisor candidate reranker.</summary>
public sealed class AdvisorCandidateRanker
{
    private const int ShortBookPageLimit = 450;
    private const int LongBookPageFloor = 180;

    /// <summary>Ranks candidates against extracted intent with stable tie breaking.</summary>
    /// <param name="candidates">Already availability-checked local catalogue candidates.</param>
    /// <param name="intent">Structured advisor intent.</param>
    /// <param name="maxResults">Maximum candidates to return.</param>
    /// <param name="comparisonReference">Optional locally resolved reference work.</param>
    public static IReadOnlyList<BookMetadataDto> Rank(
        IReadOnlyList<BookMetadataDto> candidates,
        AdvisorIntent intent,
        int maxResults = 50,
        BookMetadataDto? comparisonReference = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(intent);
        if (maxResults is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "Advisor candidate count must be between 1 and 50.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<ScoredCandidate> ordered = candidates
            .Where(candidate => seen.Add(candidate.BookId))
            .Where(candidate => !HasNegativeMatch(candidate, intent.NegativeTerms))
            .Where(candidate => MatchesKnownLength(candidate, intent.Length))
            .Select(candidate => new ScoredCandidate(candidate, Score(candidate, intent, comparisonReference)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Candidate.BookId, StringComparer.Ordinal)
            .ToList();

        List<BookMetadataDto> result = [];
        HashSet<string> authors = new(StringComparer.OrdinalIgnoreCase);
        while (result.Count < maxResults && ordered.Count > 0)
        {
            int selectedIndex = 0;
            if (intent.IsBroadDiscovery)
            {
                double strongestScore = ordered[0].Score;
                for (int i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].Score < strongestScore - 0.15)
                    {
                        break;
                    }

                    if (!authors.Contains(AuthorKey(ordered[i].Candidate)))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            ScoredCandidate selected = ordered[selectedIndex];
            result.Add(selected.Candidate);
            authors.Add(AuthorKey(selected.Candidate));
            ordered.RemoveAt(selectedIndex);
        }

        return result;
    }

    /// <summary>Returns the normalized deterministic score used by <see cref="Rank"/>.</summary>
    public static double Score(
        BookMetadataDto candidate,
        AdvisorIntent intent,
        BookMetadataDto? comparisonReference = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(intent);

        IReadOnlyList<string> positiveTerms = intent.PositiveTerms;
        if (positiveTerms.Count == 0 && !intent.IsBroadDiscovery)
        {
            return Math.Clamp(
                ConstraintScore(candidate, intent) + ComparisonScore(candidate, comparisonReference),
                0.0,
                1.0);
        }

        Dictionary<string, string> fields = Fields(candidate);
        int matches = positiveTerms.Count(term => fields.Values.Any(value => ContainsTerm(value, term)));
        double score = positiveTerms.Count == 0 ? 0.35 : matches / (double)positiveTerms.Count;
        score += ConstraintScore(candidate, intent) + ComparisonScore(candidate, comparisonReference);
        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double ComparisonScore(
        BookMetadataDto candidate,
        BookMetadataDto? reference)
    {
        if (reference is null)
        {
            return 0.0;
        }

        double score = 0.0;
        if (Overlaps(candidate.Authors, reference.Authors))
        {
            score += 0.25;
        }

        if (Overlaps(candidate.Categories, reference.Categories))
        {
            score += 0.15;
        }

        if (Overlaps(candidate.Tags, reference.Tags))
        {
            score += 0.10;
        }

        return score;
    }

    private static bool Overlaps(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right) =>
        left.Any(item => right.Contains(item, StringComparer.OrdinalIgnoreCase));

    /// <summary>Returns the local fields that match positive intent terms.</summary>
    public static IReadOnlyList<RecommendationMatchField> MatchedFields(BookMetadataDto candidate, AdvisorIntent intent)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(intent);

        Dictionary<string, string> fields = Fields(candidate);
        List<RecommendationMatchField> matched = [];
        if (intent.PositiveTerms.Any(term => ContainsTerm(fields["title"], term)))
        {
            matched.Add(RecommendationMatchField.Title);
        }

        if (intent.PositiveTerms.Any(term => ContainsTerm(fields["authors"], term)))
        {
            matched.Add(RecommendationMatchField.Author);
        }

        if (intent.PositiveTerms.Any(term => fields["tags"].Split(' ').Contains(term, StringComparer.Ordinal)))
        {
            matched.Add(RecommendationMatchField.Tags);
        }

        if (intent.PositiveTerms.Any(term => ContainsTerm(fields["description"], term)))
        {
            matched.Add(RecommendationMatchField.Description);
        }

        return matched;
    }

    private static double ConstraintScore(BookMetadataDto candidate, AdvisorIntent intent)
    {
        double score = 0.0;
        if (intent.Difficulty is not null && HasDifficultySignal(candidate, intent.Difficulty.Value))
        {
            score += 0.2;
        }

        if (intent.Length != AdvisorLengthPreference.Any && HasLengthSignal(candidate, intent.Length))
        {
            score += 0.2;
        }

        if (intent.MoodTerms.Count > 0 && intent.MoodTerms.Any(term => Fields(candidate).Values.Any(value => ContainsTerm(value, term))))
        {
            score += 0.15;
        }

        return score;
    }

    private static bool HasNegativeMatch(BookMetadataDto candidate, IReadOnlyList<string> negativeTerms) =>
        negativeTerms.Count > 0 && negativeTerms.Any(term => Fields(candidate).Values.Any(value => ContainsTerm(value, term)));

    private static bool HasDifficultySignal(BookMetadataDto candidate, DifficultyLabel difficulty)
    {
        string text = string.Join(' ', Fields(candidate).Values);
        return difficulty switch
        {
            DifficultyLabel.Introductory => ContainsAny(text, "beginner", "introductory", "accessible", "basics", "introduction"),
            DifficultyLabel.Foundational => ContainsAny(text, "foundation", "foundational", "basics", "introduction"),
            DifficultyLabel.Intermediate => ContainsAny(text, "intermediate", "applied"),
            DifficultyLabel.Advanced => ContainsAny(text, "advanced", "theory", "complex"),
            DifficultyLabel.Expert => ContainsAny(text, "expert", "specialist", "graduate", "research"),
            _ => false,
        };
    }

    private static bool HasLengthSignal(BookMetadataDto candidate, AdvisorLengthPreference length) =>
        candidate.PageCount is null
            ? length == AdvisorLengthPreference.LongBook && ContainsAny(string.Join(' ', Fields(candidate).Values), "complete", "handbook", "encyclopedia")
            : length == AdvisorLengthPreference.ShortBook
                ? candidate.PageCount <= ShortBookPageLimit
                : candidate.PageCount >= LongBookPageFloor;

    private static bool MatchesKnownLength(BookMetadataDto candidate, AdvisorLengthPreference length) =>
        length == AdvisorLengthPreference.Any ||
        candidate.PageCount is null ||
        (length == AdvisorLengthPreference.ShortBook && candidate.PageCount <= ShortBookPageLimit) ||
        (length == AdvisorLengthPreference.LongBook && candidate.PageCount >= LongBookPageFloor);

    private static Dictionary<string, string> Fields(BookMetadataDto candidate) => new(StringComparer.Ordinal)
    {
        ["title"] = candidate.Title ?? string.Empty,
        ["authors"] = string.Join(' ', candidate.Authors),
        ["tags"] = string.Join(' ', candidate.Tags),
        ["categories"] = string.Join(' ', candidate.Categories),
        ["description"] = string.Join(' ', candidate.Description, candidate.Notes),
    };

    private static bool ContainsTerm(string value, string term)
    {
        string[] termWords = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (termWords.Length > 1)
        {
            return value.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        return value
            .Split([' ', ',', ';', '.', ':', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(word => string.Equals(word, term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => ContainsTerm(value, term));

    private static string AuthorKey(BookMetadataDto candidate) =>
        candidate.Authors.Count > 0 ? candidate.Authors[0] : candidate.BookId;

    private sealed record ScoredCandidate(BookMetadataDto Candidate, double Score);
}
