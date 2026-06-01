using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP5 match-location and confidence-label tests.
/// </summary>
public sealed class MatchLocationServiceTests
{
    [Fact]
    public void MatchLocation_TitleMatch_ReturnsTitle()
    {
        var service = new MatchLocationService();
        CombinedSearchResult exact = Exact("book-title", ["title:exact"]);

        IReadOnlyList<MatchLocation> locations = service.GetLocations(exact, semanticResult: null);

        Assert.Equal([MatchLocation.Title], locations);
    }

    [Fact]
    public void MatchLocation_AuthorAndDescriptionMatch_ReturnsBoth()
    {
        var service = new MatchLocationService();
        CombinedSearchResult exact = Exact("book-meta", ["author", "description"]);

        IReadOnlyList<MatchLocation> locations = service.GetLocations(exact, semanticResult: null);

        Assert.Equal([MatchLocation.Author, MatchLocation.Description], locations);
    }

    [Fact]
    public void MatchLocation_NoteChunk_ReturnsNotePage()
    {
        var service = new MatchLocationService();
        CombinedSearchResult exact = Exact(
            "book-note",
            [],
            Fts("book-note", SearchChunkSource.Note));

        IReadOnlyList<MatchLocation> locations = service.GetLocations(exact, semanticResult: null);

        Assert.Equal([MatchLocation.NotePage], locations);
    }

    [Fact]
    public void MatchLocation_MultipleMatches_ReturnsOrderedDistinctLocations()
    {
        var service = new MatchLocationService();
        CombinedSearchResult exact = Exact(
            "book-multi",
            ["title:prefix", "tag", "full-text:page"],
            Fts("book-multi", SearchChunkSource.Page),
            Fts("book-multi", SearchChunkSource.Toc),
            Fts("book-multi", SearchChunkSource.Tag));
        SemanticSearchResult semantic = Semantic("book-multi", 0.9f);

        IReadOnlyList<MatchLocation> locations = service.GetLocations(exact, semantic);

        Assert.Equal(
            [
                MatchLocation.Title,
                MatchLocation.Tag,
                MatchLocation.Toc,
                MatchLocation.TextPage,
                MatchLocation.Semantic,
            ],
            locations);
    }

    [Fact]
    public void MatchLocation_SemanticOnly_ReturnsSemantic()
    {
        var service = new MatchLocationService();
        SemanticSearchResult semantic = Semantic("book-semantic", 0.8f);

        IReadOnlyList<MatchLocation> locations = service.GetLocations(null, semantic);

        Assert.Equal([MatchLocation.Semantic], locations);
    }

    [Fact]
    public void MatchLocation_LowSemanticScore_DoesNotReturnSemantic()
    {
        var service = new MatchLocationService();
        SemanticSearchResult semantic = Semantic("book-low", 0.1f);

        IReadOnlyList<MatchLocation> locations = service.GetLocations(null, semantic);

        Assert.Empty(locations);
    }

    [Theory]
    [InlineData(0.8, ConfidenceLabel.High)]
    [InlineData(0.5, ConfidenceLabel.Medium)]
    [InlineData(0.49, ConfidenceLabel.Low)]
    public void ConfidenceLabel_UsesHybridScoreThresholds(double score, ConfidenceLabel expected)
    {
        Assert.Equal(expected, MatchLocationService.LabelForScore(score));
    }

    [Fact]
    public void Enrich_ReturnsLocationsScoresAndConfidence()
    {
        var service = new MatchLocationService();
        CombinedSearchResult exact = Exact("book-enrich", ["title"], Fts("book-enrich", SearchChunkSource.Note));
        SemanticSearchResult semantic = Semantic("book-enrich", 0.7f);
        var result = new HybridRankedResult(
            "book-enrich",
            Title: "Enriched",
            Author: "Author",
            HybridScore: 0.85,
            ExactScore: 1.0,
            RecencyScore: 0.0,
            StatusScore: 0.0,
            RatingScore: 0.0,
            SemanticScore: 0.85,
            ExactResult: exact,
            SemanticResult: semantic);

        SearchResultEnrichment enrichment = service.Enrich(result);

        Assert.Equal("book-enrich", enrichment.BookId);
        Assert.Equal(ConfidenceLabel.High, enrichment.ConfidenceLabel);
        Assert.Equal(0.85, enrichment.HybridScore);
        Assert.Equal(0.85, enrichment.SemanticScore);
        Assert.Equal(
            [MatchLocation.Title, MatchLocation.NotePage, MatchLocation.Semantic],
            enrichment.MatchLocations);
    }

    private static CombinedSearchResult Exact(
        string bookId,
        IReadOnlyList<string> matchedFields,
        params FtsSearchResult[] hits) =>
        new(
            BookId: bookId,
            Title: bookId,
            Author: "Author",
            Score: 10,
            MatchedFields: matchedFields,
            FtsHits: hits);

    private static FtsSearchResult Fts(string bookId, SearchChunkSource source) =>
        new(
            BookId: bookId,
            Title: bookId,
            Author: "Author",
            ChunkId: 1,
            PageIndex: 0,
            ChunkIndex: 0,
            Source: source,
            Snippet: "snippet",
            Score: 5);

    private static SemanticSearchResult Semantic(string bookId, float score) =>
        new(bookId, Title: bookId, ChunkId: 1, Source: SearchChunkSource.Page, Snippet: "snippet", SemanticScore: score, ExactFallback: false);
}
