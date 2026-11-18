using System.Globalization;
using System.Text;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Builds a safe FTS5 MATCH expression from a parsed query (Sept-23 Phase 13, task 13.3,
/// K40). Bare words are combined with AND in any order, the last bare word is a prefix,
/// quoted phrases stay phrases and excluded words become NOT. Every token is emitted as a
/// quoted FTS5 string of letters and digits only, so user input can never inject FTS5
/// syntax (<c>NEAR(</c>, <c>*</c>, <c>:</c>, column filters or unbalanced quotes).
/// Diacritic folding is left to the index tokenizer (<c>unicode61 remove_diacritics 1</c>),
/// which folds the query and the indexed text the same way.
/// </summary>
internal static class FtsMatchQueryBuilder
{
    private const int MinimumPrefixLength = 2;

    /// <summary>Builds the expression; returns an empty string when nothing can match.</summary>
    public static string Build(
        IReadOnlyList<string> terms,
        IReadOnlyList<string> phrases,
        IReadOnlyList<string> exclusions)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(exclusions);

        var positives = new List<string>();
        int lastTerm = terms.Count - 1;
        for (int index = 0; index < terms.Count; index++)
        {
            IReadOnlyList<string> tokens = Tokens(terms[index]);
            if (tokens.Count == 0)
            {
                continue;
            }

            string expression = Quote(tokens);
            if (index == lastTerm && phrases.Count == 0 && tokens[^1].Length >= MinimumPrefixLength)
            {
                expression += "*";
            }

            positives.Add(expression);
        }

        foreach (string phrase in phrases)
        {
            IReadOnlyList<string> tokens = Tokens(phrase);
            if (tokens.Count > 0)
            {
                positives.Add(Quote(tokens));
            }
        }

        if (positives.Count == 0)
        {
            return string.Empty;
        }

        string positive = string.Join(" AND ", positives);
        List<string> negatives = exclusions
            .Select(Tokens)
            .Where(tokens => tokens.Count > 0)
            .Select(Quote)
            .ToList();
        return negatives.Count == 0
            ? positive
            : "(" + positive + ") NOT (" + string.Join(" OR ", negatives) + ")";
    }

    /// <summary>Builds the expression for a parsed query and optional source-field values.</summary>
    public static string Build(ParsedSearchQuery query, IEnumerable<string>? extraTerms = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        List<string> terms = [.. query.Terms];
        if (extraTerms is not null)
        {
            terms.InsertRange(0, extraTerms);
        }

        return Build(terms, query.Phrases, query.Exclusions);
    }

    /// <summary>Lower-cased letter-or-digit runs of <paramref name="text"/> (no diacritic folding).</summary>
    internal static IReadOnlyList<string> Tokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var tokens = new List<string>();
        var token = new StringBuilder();
        foreach (char ch in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(ch))
            {
                token.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
            }
            else if (token.Length > 0)
            {
                tokens.Add(token.ToString());
                token.Clear();
            }
        }

        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }

    private static string Quote(IReadOnlyList<string> tokens) =>
        "\"" + string.Join(' ', tokens) + "\"";
}
