using System.Text;

namespace OgmaLibrary.Application.Search;

/// <summary>Structured fields a search query can name with <c>field:value</c>.</summary>
public enum SearchField
{
    /// <summary><c>title:</c> — the book title.</summary>
    Title = 0,

    /// <summary><c>author:</c> — an author name.</summary>
    Author = 1,

    /// <summary><c>isbn:</c> — an ISBN, with or without hyphens.</summary>
    Isbn = 2,

    /// <summary><c>year:</c> — a publication year or range (<c>1990-1999</c>).</summary>
    Year = 3,

    /// <summary><c>tag:</c> — a tag or subject.</summary>
    Tag = 4,

    /// <summary><c>shelf:</c> — a shelf name.</summary>
    Shelf = 5,

    /// <summary><c>description:</c> — the description or summary.</summary>
    Description = 6,

    /// <summary><c>text:</c> or <c>page:</c> — the book's page text only.</summary>
    Text = 7,

    /// <summary><c>note:</c> — the reader's own notes.</summary>
    Note = 8,

    /// <summary><c>toc:</c> or <c>contents:</c> — table-of-contents entries.</summary>
    Toc = 9,
}

/// <summary>One structured filter from a query, such as <c>author:Okello</c> or <c>-tag:draft</c>.</summary>
/// <param name="Field">The named field.</param>
/// <param name="Value">The value as typed (quotes removed).</param>
/// <param name="Exclude">True when the filter was negated with a leading minus sign.</param>
public sealed record SearchFieldFilter(SearchField Field, string Value, bool Exclude = false);

/// <summary>
/// A parsed search query (Sept-23 Phase 13). Bare words must all match (any order),
/// quoted text stays a phrase, <c>field:value</c> restricts a field and a leading minus
/// excludes a word, phrase or field value.
/// </summary>
/// <param name="Text">The query text that was parsed (after any truncation).</param>
/// <param name="Terms">Bare words in typed order.</param>
/// <param name="Phrases">Quoted phrases.</param>
/// <param name="Exclusions">Excluded words and phrases.</param>
/// <param name="Filters">Structured field filters.</param>
/// <param name="WasTruncated">True when the input exceeded <see cref="SearchQueryParser.MaxQueryLength"/>.</param>
public sealed record ParsedSearchQuery(
    string Text,
    IReadOnlyList<string> Terms,
    IReadOnlyList<string> Phrases,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<SearchFieldFilter> Filters,
    bool WasTruncated)
{
    /// <summary>An empty query.</summary>
    public static ParsedSearchQuery Empty { get; } = new(string.Empty, [], [], [], [], false);

    /// <summary>Whether the query has any bare word or phrase.</summary>
    public bool HasFreeText => Terms.Count > 0 || Phrases.Count > 0;

    /// <summary>Whether the query has nothing that could match.</summary>
    public bool IsEmpty => !HasFreeText && !Filters.Any(filter => !filter.Exclude);

    /// <summary>Folded word tokens of the bare words and phrases, for metadata matching.</summary>
    public IReadOnlyList<string> FreeTextTokens =>
        Terms.Concat(Phrases).SelectMany(SearchTextNormalizer.Tokens).ToList();

    /// <summary>Filters for one field that include (not exclude) matches.</summary>
    public IEnumerable<SearchFieldFilter> Including(SearchField field) =>
        Filters.Where(filter => filter.Field == field && !filter.Exclude);
}

/// <summary>Parses the search box language (Sept-23 Phase 13, task 13.2).</summary>
public static class SearchQueryParser
{
    /// <summary>Longest query parsed; longer input is truncated and flagged.</summary>
    public const int MaxQueryLength = 256;

    private static readonly Dictionary<string, SearchField> FieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = SearchField.Title,
        ["author"] = SearchField.Author,
        ["by"] = SearchField.Author,
        ["isbn"] = SearchField.Isbn,
        ["year"] = SearchField.Year,
        ["tag"] = SearchField.Tag,
        ["tags"] = SearchField.Tag,
        ["subject"] = SearchField.Tag,
        ["shelf"] = SearchField.Shelf,
        ["description"] = SearchField.Description,
        ["text"] = SearchField.Text,
        ["page"] = SearchField.Text,
        ["note"] = SearchField.Note,
        ["notes"] = SearchField.Note,
        ["toc"] = SearchField.Toc,
        ["contents"] = SearchField.Toc,
    };

    /// <summary>Parses <paramref name="query"/>; never throws for any input.</summary>
    public static ParsedSearchQuery Parse(string? query)
    {
        string text = (query ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();
        bool truncated = false;
        if (text.Length > MaxQueryLength)
        {
            text = text[..MaxQueryLength].TrimEnd();
            truncated = true;
        }

        if (text.Length == 0)
        {
            return ParsedSearchQuery.Empty;
        }

        var terms = new List<string>();
        var phrases = new List<string>();
        var exclusions = new List<string>();
        var filters = new List<SearchFieldFilter>();
        int index = 0;
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            bool exclude = false;
            if (text[index] == '-' && index + 1 < text.Length && !char.IsWhiteSpace(text[index + 1]))
            {
                exclude = true;
                index++;
            }

            if (text[index] == '"')
            {
                string phrase = ReadQuoted(text, ref index);
                AddPhrase(phrase, exclude, phrases, exclusions);
                continue;
            }

            int start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '"' && text[index] != ':')
            {
                index++;
            }

            if (index < text.Length && text[index] == ':' &&
                FieldNames.TryGetValue(text[start..index], out SearchField field))
            {
                index++;
                string value = index < text.Length && text[index] == '"'
                    ? ReadQuoted(text, ref index)
                    : ReadWord(text, ref index);
                if (HasWordCharacter(value))
                {
                    filters.Add(new SearchFieldFilter(field, value.Trim(), exclude));
                }
                else if (!exclude)
                {
                    // "author:" with nothing after it is just the word.
                    terms.Add(text[start..index]);
                }

                continue;
            }

            // Not a known field: the colon (and anything after it) belongs to the word.
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '"')
            {
                index++;
            }

            string word = text[start..index];
            if (!HasWordCharacter(word))
            {
                continue;
            }

            if (exclude)
            {
                exclusions.Add(word);
            }
            else
            {
                terms.Add(word);
            }
        }

        return new ParsedSearchQuery(text, terms, phrases, exclusions, filters, truncated);
    }

    /// <summary>
    /// Parses a <c>year:</c> value: <c>1998</c>, <c>1990-1999</c>, <c>1990..1999</c>,
    /// <c>&gt;2000</c> or <c>&lt;1950</c>. Returns false for anything else.
    /// </summary>
    public static bool TryParseYearRange(string value, out int from, out int to)
    {
        from = int.MinValue;
        to = int.MaxValue;
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.StartsWith('>') && int.TryParse(trimmed.AsSpan(1).TrimStart('='), out int lower))
        {
            from = trimmed.Length > 1 && trimmed[1] == '=' ? lower : lower + 1;
            return true;
        }

        if (trimmed.StartsWith('<') && int.TryParse(trimmed.AsSpan(1).TrimStart('='), out int upper))
        {
            to = trimmed.Length > 1 && trimmed[1] == '=' ? upper : upper - 1;
            return true;
        }

        string[] parts = trimmed.Split(["..", "-", "–"], 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int start) &&
            int.TryParse(parts[1], out int end) &&
            start <= end)
        {
            from = start;
            to = end;
            return true;
        }

        if (parts.Length == 1 && int.TryParse(parts[0], out int year))
        {
            from = year;
            to = year;
            return true;
        }

        return false;
    }

    private static void AddPhrase(string phrase, bool exclude, List<string> phrases, List<string> exclusions)
    {
        string trimmed = phrase.Trim();
        if (!HasWordCharacter(trimmed))
        {
            return;
        }

        (exclude ? exclusions : phrases).Add(trimmed);
    }

    private static string ReadQuoted(string text, ref int index)
    {
        int start = index + 1;
        int end = text.IndexOf('"', start);
        if (end < 0)
        {
            index = text.Length;
            return text[start..];
        }

        index = end + 1;
        return text[start..end];
    }

    private static string ReadWord(string text, ref int index)
    {
        int start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return text[start..index];
    }

    private static bool HasWordCharacter(string value) => value.Any(char.IsLetterOrDigit);
}
