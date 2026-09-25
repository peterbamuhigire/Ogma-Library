using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Sept-23 Phase 13 (task 13.2/13.3): query language, folding and FTS5 match building.</summary>
public sealed class SearchQueryParserTests
{
    public static TheoryData<string, string> ParseCases => new()
    {
        // input → "terms|phrases|exclusions|filters"
        { "Lantern", "Lantern|||" },
        { "  lantern   keeper ", "lantern,keeper|||" },
        { "dhow Zanzibar", "dhow,Zanzibar|||" },
        { "\"Chwezi dynasty\"", "|Chwezi dynasty||" },
        { "\"unterminated phrase", "|unterminated phrase||" },
        { "author:Okello", "|||Author=Okello" },
        { "AUTHOR:Okello", "|||Author=Okello" },
        { "by:Okello", "|||Author=Okello" },
        { "title:\"Tropical Ecology\"", "|||Title=Tropical Ecology" },
        { "isbn:978-0-306-40615-7", "|||Isbn=978-0-306-40615-7" },
        { "year:1990-1999", "|||Year=1990-1999" },
        { "tag:optics light", "light|||Tag=optics" },
        { "shelf:Favourites", "|||Shelf=Favourites" },
        { "note:margin", "|||Note=margin" },
        { "text:caravan", "|||Text=caravan" },
        { "toc:Chapter", "|||Toc=Chapter" },
        { "lantern -amber", "lantern||amber|" },
        { "lantern -\"amber light\"", "lantern||amber light|" },
        { "-author:Okello light", "light|||-Author=Okello" },
        { "foo:bar", "foo:bar|||" },
        { "10:30 train", "10:30,train|||" },
        { "author:", "author:|||" },
        { "***", "|||" },
        { "\"\"", "|||" },
        { "-", "|||" },
        { "NEAR(a b)", "NEAR(a,b)|||" },
        { "a OR b", "a,OR,b|||" },
        { "Wanjirũ", "Wanjirũ|||" },
        { "e-book reader", "e-book,reader|||" },
        { "title:Algo author:Namutebi graphs", "graphs|||Title=Algo;Author=Namutebi" },
    };

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void Parse_Table(string input, string expected)
    {
        ParsedSearchQuery parsed = SearchQueryParser.Parse(input);
        string actual = string.Join(',', parsed.Terms) + "|" +
            string.Join(',', parsed.Phrases) + "|" +
            string.Join(',', parsed.Exclusions) + "|" +
            string.Join(';', parsed.Filters.Select(filter => (filter.Exclude ? "-" : string.Empty) + filter.Field + "=" + filter.Value));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Parse_TruncatesVeryLongQueries()
    {
        ParsedSearchQuery parsed = SearchQueryParser.Parse(new string('a', 1000));

        Assert.True(parsed.WasTruncated);
        Assert.Equal(SearchQueryParser.MaxQueryLength, parsed.Text.Length);
    }

    [Theory]
    [InlineData("Wanjirũ", "wanjiru")]
    [InlineData("WANJIRU", "wanjiru")]
    [InlineData("Évening", "evening")]
    [InlineData("Ngũgĩ", "ngugi")]
    [InlineData("Straße", "strasse")]
    [InlineData("Łódź", "lodz")]
    public void Fold_RemovesCaseAndDiacritics(string input, string expected) =>
        Assert.Equal(expected, SearchTextNormalizer.Fold(input));

    [Fact]
    public void Fold_TreatsDecomposedAndPrecomposedAlike() =>
        Assert.Equal(SearchTextNormalizer.Fold("Wanjirũ"), SearchTextNormalizer.Fold("Wanjirũ"));

    [Theory]
    [InlineData("algoritms", "algorithms", 1)]
    [InlineData("explaned", "explained", 1)]
    [InlineData("teh", "the", 1)]
    [InlineData("lantern", "lantern", 0)]
    [InlineData("zzqxnotaword", "algorithms", 3)]
    public void EditDistance_IsBounded(string source, string target, int expected) =>
        Assert.Equal(expected, SearchTextNormalizer.EditDistance(source, target, 2) is var d && d > 2 ? 3 : d);

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    public void TypoBudget_ScalesWithLength(int length, int expected) =>
        Assert.Equal(expected, SearchTextNormalizer.TypoBudget(length));

    [Theory]
    [InlineData("dhow Zanzibar", "\"dhow\" AND \"zanzibar\"*")]
    [InlineData("\"Chwezi dynasty\"", "\"chwezi dynasty\"")]
    [InlineData("lantern -amber", "(\"lantern\"*) NOT (\"amber\")")]
    [InlineData("e-book", "\"e book\"*")]
    [InlineData("NEAR(a b)", "\"near a\" AND \"b\"")]
    [InlineData("***", "")]
    [InlineData("-amber", "")]
    [InlineData("author:Okello", "")]
    public void FtsMatch_UsesAndSemanticsAndQuotesEveryToken(string input, string expected) =>
        Assert.Equal(expected, FtsMatchQueryBuilder.Build(SearchQueryParser.Parse(input)));

    [Theory]
    [InlineData("1998", 1998, 1998)]
    [InlineData("1990-1999", 1990, 1999)]
    [InlineData("1990..1999", 1990, 1999)]
    [InlineData(">2000", 2001, int.MaxValue)]
    [InlineData("<=1950", int.MinValue, 1950)]
    public void YearRange_Parses(string value, int from, int to)
    {
        Assert.True(SearchQueryParser.TryParseYearRange(value, out int actualFrom, out int actualTo));
        Assert.Equal(from, actualFrom);
        Assert.Equal(to, actualTo);
    }
}
