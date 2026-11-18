using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>One metadata match for a book.</summary>
internal sealed record MetadataHit(
    string BookId,
    double Score,
    IReadOnlyList<UnifiedMatch> Matches,
    string? CorrectionSuggestion,
    bool IsFuzzy);

/// <summary>Result of metadata matching: hits and the structured-filter restriction.</summary>
/// <param name="Hits">Exact and prefix matches, best first.</param>
/// <param name="FuzzyHits">Typo-tolerant matches, best first (used only as a fallback).</param>
/// <param name="Allowed">Books that satisfy the structured filters, or null when unrestricted.</param>
/// <param name="Excluded">Books removed by exclusions on title or author.</param>
internal sealed record MetadataOutcome(
    IReadOnlyList<MetadataHit> Hits,
    IReadOnlyList<MetadataHit> FuzzyHits,
    IReadOnlySet<string>? Allowed,
    IReadOnlySet<string> Excluded);

/// <summary>
/// Diacritic- and case-insensitive metadata matching with prefix and typo tolerance over
/// title, author, ISBN, year, tags, shelves and file name (Sept-23 Phase 13, task 13.4).
/// All free-text words must be found (in any of those fields); typo tolerance allows an
/// edit distance of 0 for words up to 3 characters, 1 for 4–7 and 2 from 8.
/// </summary>
internal static class MetadataMatcher
{
    private const int MinimumPrefix = 3;
    private const int MinimumLastPrefix = 2;

    /// <summary>Matches <paramref name="query"/> against <paramref name="books"/>.</summary>
    public static MetadataOutcome Match(ParsedSearchQuery query, IReadOnlyList<SearchBookCandidate> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        return Match(query, Prepare(books));
    }

    /// <summary>Folds the matching fields of <paramref name="books"/> once.</summary>
    public static IReadOnlyList<FoldedSearchBook> Prepare(IReadOnlyList<SearchBookCandidate> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        return books.Select(book => new FoldedSearchBook(book)).ToList();
    }

    /// <summary>Matches <paramref name="query"/> against prepared books.</summary>
    public static MetadataOutcome Match(ParsedSearchQuery query, IReadOnlyList<FoldedSearchBook> prepared)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(prepared);
        IReadOnlyList<SearchBookCandidate> books = prepared.Select(book => book.Source).ToList();

        IReadOnlyList<string> tokens = query.FreeTextTokens;
        bool lastIsPrefix = query.Terms.Count > 0 && query.Phrases.Count == 0;
        List<SearchFieldFilter> metadataFilters = query.Filters.Where(filter => IsMetadataField(filter.Field)).ToList();
        bool textSourceOnly = query.Filters.Any(filter => !filter.Exclude && !IsMetadataField(filter.Field));
        List<IReadOnlyList<string>> exclusions = query.Exclusions
            .Select(SearchTextNormalizer.Tokens)
            .Where(list => list.Count > 0)
            .ToList();

        var hits = new List<MetadataHit>();
        var fuzzyHits = new List<MetadataHit>();
        HashSet<string>? allowed = metadataFilters.Any(filter => !filter.Exclude) ? new(StringComparer.Ordinal) : null;
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (FoldedSearchBook fields in prepared)
        {
            SearchBookCandidate book = fields.Source;
            if (exclusions.Any(exclusion => fields.CoversAll(exclusion, FieldGroup.TitleAuthor, lastIsPrefix: false)))
            {
                excluded.Add(book.BookId);
                continue;
            }

            if (!PassesFilters(fields, metadataFilters, out List<UnifiedMatch> filterMatches, out bool filterFuzzy))
            {
                if (metadataFilters.Count > 0)
                {
                    excluded.Add(book.BookId);
                }

                continue;
            }

            allowed?.Add(book.BookId);
            if (tokens.Count == 0 || textSourceOnly)
            {
                if (filterMatches.Count > 0)
                {
                    MetadataHit filterHit = new(book.BookId, filterFuzzy ? 50 : 100, filterMatches, filterFuzzy ? book.DisplayTitle : null, filterFuzzy);
                    (filterFuzzy ? fuzzyHits : hits).Add(filterHit);
                }

                continue;
            }

            if (MatchFreeText(fields, tokens, lastIsPrefix, query) is { } hit)
            {
                List<UnifiedMatch> matches = [.. filterMatches, .. hit.Matches];
                MetadataHit combined = hit with { Matches = Distinct(matches), IsFuzzy = hit.IsFuzzy || filterFuzzy };
                (combined.IsFuzzy ? fuzzyHits : hits).Add(combined);
            }
        }

        return new MetadataOutcome(Order(hits, books), Order(fuzzyHits, books), allowed, excluded);
    }

    private static List<MetadataHit> Order(List<MetadataHit> hits, IReadOnlyList<SearchBookCandidate> books)
    {
        Dictionary<string, string> titles = books.ToDictionary(book => book.BookId, book => book.DisplayTitle, StringComparer.Ordinal);
        return hits
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => titles[hit.BookId], StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(hit => hit.BookId, StringComparer.Ordinal)
            .ToList();
    }

    private static MetadataHit? MatchFreeText(
        FoldedSearchBook book,
        IReadOnlyList<string> tokens,
        bool lastIsPrefix,
        ParsedSearchQuery query)
    {
        // ISBN typed with or without hyphens: all-digit input compared as one number.
        if (tokens.All(token => token.All(char.IsDigit)) && book.Source.IsbnDigits is { } isbn)
        {
            string digits = string.Concat(tokens);
            if (digits.Length >= 4 && isbn.Contains(digits, StringComparison.Ordinal))
            {
                return new MetadataHit(book.Source.BookId, 90 + (digits.Length >= 10 ? 60 : 0), [new UnifiedMatch(UnifiedMatchKind.Isbn)], null, false);
            }
        }

        var kinds = new List<UnifiedMatchKind>();
        double score = 0;
        var uncovered = new List<int>();
        for (int index = 0; index < tokens.Count; index++)
        {
            bool allowPrefix = index == tokens.Count - 1 && lastIsPrefix
                ? tokens[index].Length >= MinimumLastPrefix
                : tokens[index].Length >= MinimumPrefix;
            UnifiedMatchKind? kind = book.BestField(tokens[index], allowPrefix);
            if (kind is null)
            {
                uncovered.Add(index);
                continue;
            }

            kinds.Add(kind.Value);
            score += Weight(kind.Value);
        }

        score /= tokens.Count;
        if (uncovered.Count == 0)
        {
            foreach (string phrase in query.Phrases)
            {
                string folded = string.Join(' ', SearchTextNormalizer.Tokens(phrase));
                if (!book.ContainsPhrase(folded))
                {
                    return null;
                }
            }

            if (book.CoversAll(tokens, FieldGroup.Title, lastIsPrefix))
            {
                score += 40;
                if (book.TitleEquals(tokens))
                {
                    score += 50;
                }
                else if (book.TitleStartsWith(tokens))
                {
                    score += 10;
                }
            }
            else if (book.CoversAll(tokens, FieldGroup.Author, lastIsPrefix))
            {
                score += 30;
            }

            return new MetadataHit(book.Source.BookId, score, Kinds(kinds), null, false);
        }

        // Typo tolerance on title and author words only.
        int totalDistance = 0;
        UnifiedMatchKind? fuzzyKind = null;
        foreach (int index in uncovered)
        {
            string token = tokens[index];
            int budget = SearchTextNormalizer.TypoBudget(token.Length);
            if (budget == 0 || book.ClosestTitleAuthor(token, budget) is not { } closest)
            {
                return null;
            }

            totalDistance += closest.Distance;
            fuzzyKind ??= closest.Kind;
            kinds.Add(closest.Kind);
        }

        double fuzzyScore = Math.Max(1, (score * 0.5) + 30 - (5 * totalDistance));
        string suggestion = fuzzyKind == UnifiedMatchKind.Author && book.Source.DisplayAuthor is { } author
            ? author
            : book.Source.DisplayTitle;
        return new MetadataHit(
            book.Source.BookId,
            fuzzyScore,
            Kinds(kinds).Select(match => match with { IsFuzzy = true }).ToList(),
            suggestion,
            true);
    }

    private static bool PassesFilters(
        FoldedSearchBook book,
        IReadOnlyList<SearchFieldFilter> filters,
        out List<UnifiedMatch> matches,
        out bool usedFuzzy)
    {
        matches = [];
        usedFuzzy = false;
        foreach (SearchFieldFilter filter in filters)
        {
            bool matched = MatchesFilter(book, filter, out bool fuzzy);
            if (filter.Exclude)
            {
                if (matched)
                {
                    return false;
                }

                continue;
            }

            if (!matched)
            {
                return false;
            }

            usedFuzzy |= fuzzy;
            matches.Add(new UnifiedMatch(KindOf(filter.Field), IsFuzzy: fuzzy));
        }

        return true;
    }

    private static bool MatchesFilter(FoldedSearchBook book, SearchFieldFilter filter, out bool fuzzy)
    {
        fuzzy = false;
        IReadOnlyList<string> tokens = SearchTextNormalizer.Tokens(filter.Value);
        switch (filter.Field)
        {
            case SearchField.Title:
            case SearchField.Author:
                FieldGroup group = filter.Field == SearchField.Title ? FieldGroup.Title : FieldGroup.Author;
                if (tokens.Count > 0 && book.CoversAll(tokens, group, lastIsPrefix: true))
                {
                    return true;
                }

                fuzzy = tokens.Count > 0 && tokens.All(token =>
                    book.CoversToken(token, group, allowPrefix: token.Length >= MinimumPrefix) ||
                    (SearchTextNormalizer.TypoBudget(token.Length) is > 0 and int budget &&
                     book.Closest(token, group, budget) is not null));
                return fuzzy;
            case SearchField.Isbn:
                string digits = new(filter.Value.Where(ch => char.IsDigit(ch) || ch is 'X' or 'x').Select(char.ToUpperInvariant).ToArray());
                return digits.Length >= 4 && book.Source.IsbnDigits?.Contains(digits, StringComparison.Ordinal) == true;
            case SearchField.Year:
                return SearchQueryParser.TryParseYearRange(filter.Value, out int from, out int to) &&
                    book.Source.Year is int year && year >= from && year <= to;
            case SearchField.Tag:
                return tokens.Count > 0 && book.CoversAll(tokens, FieldGroup.Tag, lastIsPrefix: true);
            case SearchField.Shelf:
                return tokens.Count > 0 && book.CoversAll(tokens, FieldGroup.Shelf, lastIsPrefix: true);
            default:
                return true;
        }
    }

    private static bool IsMetadataField(SearchField field) =>
        field is SearchField.Title or SearchField.Author or SearchField.Isbn or
            SearchField.Year or SearchField.Tag or SearchField.Shelf;

    private static UnifiedMatchKind KindOf(SearchField field) => field switch
    {
        SearchField.Title => UnifiedMatchKind.Title,
        SearchField.Author => UnifiedMatchKind.Author,
        SearchField.Isbn => UnifiedMatchKind.Isbn,
        SearchField.Year => UnifiedMatchKind.Year,
        SearchField.Tag => UnifiedMatchKind.Tag,
        SearchField.Shelf => UnifiedMatchKind.Shelf,
        _ => UnifiedMatchKind.Page,
    };

    private static double Weight(UnifiedMatchKind kind) => kind switch
    {
        UnifiedMatchKind.Title => 100,
        UnifiedMatchKind.Author => 80,
        UnifiedMatchKind.Isbn => 90,
        UnifiedMatchKind.Tag => 50,
        UnifiedMatchKind.Shelf => 40,
        _ => 30,
    };

    private static List<UnifiedMatch> Kinds(IEnumerable<UnifiedMatchKind> kinds) =>
        kinds.Distinct().OrderBy(kind => kind).Select(kind => new UnifiedMatch(kind)).ToList();

    private static List<UnifiedMatch> Distinct(IEnumerable<UnifiedMatch> matches) =>
        matches.GroupBy(match => match.Kind).Select(group => group.First()).ToList();

    internal enum FieldGroup
    {
        Title,
        Author,
        TitleAuthor,
        Tag,
        Shelf,
    }

    /// <summary>A candidate with its matching fields folded once (cached with the snapshot).</summary>
    internal sealed class FoldedSearchBook
    {
        private readonly string[] _title;
        private readonly string[][] _titles;
        private readonly string[] _author;
        private readonly string[] _tags;
        private readonly string[] _shelves;
        private readonly string[] _file;
        private readonly string _joinedText;

        public FoldedSearchBook(SearchBookCandidate source)
        {
            Source = source;
            _titles = source.Titles.Select(title => SearchTextNormalizer.Tokens(title).ToArray()).ToArray();
            _title = _titles.SelectMany(tokens => tokens).Distinct(StringComparer.Ordinal).ToArray();
            _author = source.Authors.SelectMany(SearchTextNormalizer.Tokens).Distinct(StringComparer.Ordinal).ToArray();
            _tags = source.Tags.SelectMany(SearchTextNormalizer.Tokens).Distinct(StringComparer.Ordinal).ToArray();
            _shelves = source.Shelves.SelectMany(SearchTextNormalizer.Tokens).Distinct(StringComparer.Ordinal).ToArray();
            _file = SearchTextNormalizer.Tokens(source.FileName).ToArray();
            _joinedText = string.Join(
                " | ",
                source.Titles.Concat(source.Authors).Concat(source.Tags).Concat(source.Shelves)
                    .Select(value => string.Join(' ', SearchTextNormalizer.Tokens(value))));
        }

        public SearchBookCandidate Source { get; }

        public UnifiedMatchKind? BestField(string token, bool allowPrefix)
        {
            if (Covers(_title, token, allowPrefix))
            {
                return UnifiedMatchKind.Title;
            }

            if (Covers(_author, token, allowPrefix))
            {
                return UnifiedMatchKind.Author;
            }

            if (Covers(_tags, token, allowPrefix))
            {
                return UnifiedMatchKind.Tag;
            }

            if (Covers(_shelves, token, allowPrefix))
            {
                return UnifiedMatchKind.Shelf;
            }

            // The file name stands in for a missing title (reported as a title match).
            return Covers(_file, token, allowPrefix) ? UnifiedMatchKind.Title : null;
        }

        public bool CoversToken(string token, FieldGroup group, bool allowPrefix) =>
            Tokens(group).Any(field => Covers(field, token, allowPrefix));

        public bool CoversAll(IReadOnlyList<string> tokens, FieldGroup group, bool lastIsPrefix)
        {
            for (int index = 0; index < tokens.Count; index++)
            {
                bool allowPrefix = index == tokens.Count - 1 && lastIsPrefix
                    ? tokens[index].Length >= MinimumLastPrefix
                    : tokens[index].Length >= MinimumPrefix;
                if (!CoversToken(tokens[index], group, allowPrefix))
                {
                    return false;
                }
            }

            return tokens.Count > 0;
        }

        public bool TitleEquals(IReadOnlyList<string> tokens) =>
            _titles.Any(title => title.SequenceEqual(tokens, StringComparer.Ordinal));

        public bool TitleStartsWith(IReadOnlyList<string> tokens) =>
            _titles.Any(title => title.Length >= tokens.Count &&
                title.Take(tokens.Count - 1).SequenceEqual(tokens.Take(tokens.Count - 1), StringComparer.Ordinal) &&
                title[tokens.Count - 1].StartsWith(tokens[^1], StringComparison.Ordinal));

        public bool ContainsPhrase(string foldedPhrase) =>
            foldedPhrase.Length > 0 && _joinedText.Contains(foldedPhrase, StringComparison.Ordinal);

        public (UnifiedMatchKind Kind, int Distance)? ClosestTitleAuthor(string token, int budget)
        {
            int? title = Closest(token, FieldGroup.Title, budget);
            int? author = Closest(token, FieldGroup.Author, budget);
            if (title is null && author is null)
            {
                return null;
            }

            return (author is null || (title is not null && title <= author))
                ? (UnifiedMatchKind.Title, title!.Value)
                : (UnifiedMatchKind.Author, author.Value);
        }

        public int? Closest(string token, FieldGroup group, int budget)
        {
            int best = budget + 1;
            foreach (string[] field in Tokens(group))
            {
                foreach (string candidate in field)
                {
                    // Compare whole words, and the same-length prefix of longer words so a
                    // mistyped word that is still being typed can match.
                    int distance = SearchTextNormalizer.EditDistance(token, candidate, budget);
                    if (candidate.Length > token.Length + budget)
                    {
                        distance = Math.Min(
                            distance,
                            SearchTextNormalizer.EditDistance(token, candidate[..token.Length], budget) + 1);
                    }

                    best = Math.Min(best, distance);
                }
            }

            return best <= budget ? best : null;
        }

        private IEnumerable<string[]> Tokens(FieldGroup group) => group switch
        {
            FieldGroup.Title => [_title, _file],
            FieldGroup.Author => [_author],
            FieldGroup.TitleAuthor => [_title, _author],
            FieldGroup.Tag => [_tags],
            FieldGroup.Shelf => [_shelves],
            _ => [],
        };

        private static bool Covers(string[] field, string token, bool allowPrefix)
        {
            foreach (string candidate in field)
            {
                if (string.Equals(candidate, token, StringComparison.Ordinal) ||
                    (allowPrefix && candidate.StartsWith(token, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
