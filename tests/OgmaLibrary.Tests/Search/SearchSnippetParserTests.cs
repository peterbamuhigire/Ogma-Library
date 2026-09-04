using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Regression coverage for safe FTS snippets and highlight ranges.</summary>
public sealed class SearchSnippetParserTests
{
    [Fact]
    public void Parse_RemovesOnlyHighlightMarkersAndReturnsRanges()
    {
        SearchSnippet result = SearchSnippetParser.Parse("before <b>matched</b> after");

        Assert.Equal("before matched after", result.Text);
        Assert.Equal([new SearchSnippetSpan(7, 7)], result.Spans);
    }

    [Fact]
    public void Parse_IsSafeForUnbalancedMarkers()
    {
        SearchSnippet result = SearchSnippetParser.Parse("<b>unclosed <script>");

        Assert.Equal("unclosed <script>", result.Text);
        Assert.Equal([new SearchSnippetSpan(0, 17)], result.Spans);
    }
}
