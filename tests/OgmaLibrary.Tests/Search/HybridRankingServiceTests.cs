using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP4 hybrid ranking formula and determinism tests.
/// </summary>
public sealed class HybridRankingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecencyScore_Today_ReturnsOne()
    {
        double score = HybridRankingService.RecencyScore(Now, Now);

        Assert.Equal(1.0, score, precision: 6);
    }

    [Fact]
    public void RecencyScore_ThirtyDaysAgo_ReturnsHalf()
    {
        double score = HybridRankingService.RecencyScore(Now.AddDays(-30), Now);

        Assert.Equal(0.5, score, precision: 6);
    }

    [Fact]
    public void StatusScore_MapsDomainStates()
    {
        Assert.Equal(1.0, HybridRankingService.StatusScore(ReadingStatus.Reading));
        Assert.Equal(0.5, HybridRankingService.StatusScore(ReadingStatus.Finished));
        Assert.Equal(0.0, HybridRankingService.StatusScore(ReadingStatus.Unread));
    }

    [Fact]
    public void HybridScore_SemanticAbsent_FallsBackToExactWeight()
    {
        var service = new HybridRankingService();
        var exact = new[]
        {
            Exact("book-a", score: 10),
            Exact("book-b", score: 9),
        };
        var signals = new Dictionary<string, HybridBookSignals>(StringComparer.Ordinal)
        {
            ["book-a"] = Signals("book-a", daysAgo: null, ReadingStatus.Unread, rating: null),
            ["book-b"] = Signals("book-b", daysAgo: null, ReadingStatus.Unread, rating: null),
        };

        IReadOnlyList<HybridRankedResult> results = service.Rank(
            exact,
            [],
            signals,
            HybridRankingWeights.Default,
            Now,
            limit: 10);

        Assert.Equal(["book-a", "book-b"], results.Select(result => result.BookId).ToArray());
        Assert.All(results, result => Assert.Null(result.SemanticScore));
        Assert.True(results[0].HybridScore > 0.5, "Semantic weight should be redistributed to exact ranking.");
    }

    [Fact]
    public void HybridScore_BlendsExactSemanticRecencyStatusAndRating()
    {
        var service = new HybridRankingService();
        var exact = new[] { Exact("book-a", score: 4), Exact("book-b", score: 10) };
        var semantic = new[] { Semantic("book-a", score: 1.0f), Semantic("book-b", score: -1.0f) };
        var signals = new Dictionary<string, HybridBookSignals>(StringComparer.Ordinal)
        {
            ["book-a"] = Signals("book-a", daysAgo: 0, ReadingStatus.Reading, rating: 5),
            ["book-b"] = Signals("book-b", daysAgo: 120, ReadingStatus.Unread, rating: 1),
        };

        IReadOnlyList<HybridRankedResult> results = service.Rank(
            exact,
            semantic,
            signals,
            HybridRankingWeights.Default,
            Now,
            limit: 10);

        Assert.Equal("book-a", results[0].BookId);
        Assert.True(results[0].SemanticScore > results[1].SemanticScore);
        Assert.True(results[0].RecencyScore > results[1].RecencyScore);
        Assert.True(results[0].RatingScore > results[1].RatingScore);
    }

    [Fact]
    public void HybridScore_TieBreaksByBookId()
    {
        var service = new HybridRankingService();
        var exact = new[] { Exact("book-b", score: 5), Exact("book-a", score: 5) };

        IReadOnlyList<HybridRankedResult> results = service.Rank(
            exact,
            [],
            new Dictionary<string, HybridBookSignals>(StringComparer.Ordinal),
            HybridRankingWeights.Default,
            Now,
            limit: 10);

        Assert.Equal(["book-a", "book-b"], results.Select(result => result.BookId).ToArray());
    }

    [Fact]
    public void HybridRanking_DeterministicOrder_ForOneHundredQueries()
    {
        var service = new HybridRankingService();
        HybridRankingWeights weights = HybridRankingWeights.Default;

        for (int query = 0; query < 100; query++)
        {
            IReadOnlyList<CombinedSearchResult> exact = Enumerable.Range(0, 30)
                .Select(i => Exact($"book-{i:000}", score: ((i * 17) + query) % 11))
                .Reverse()
                .ToArray();
            IReadOnlyList<SemanticSearchResult> semantic = Enumerable.Range(0, 30)
                .Select(i => Semantic($"book-{i:000}", score: ((((i * 7) + query) % 9) - 4) / 4.0f))
                .Reverse()
                .ToArray();
            var signals = Enumerable.Range(0, 30)
                .Select(i => Signals(
                    $"book-{i:000}",
                    daysAgo: (i + query) % 45,
                    (ReadingStatus)(i % 4),
                    rating: (i % 5) + 1))
                .ToDictionary(signal => signal.BookId, StringComparer.Ordinal);

            string[] first = service
                .Rank(exact, semantic, signals, weights, Now, limit: 20)
                .Select(result => result.BookId)
                .ToArray();
            string[] second = service
                .Rank(exact.Reverse().ToArray(), semantic.Reverse().ToArray(), signals, weights, Now, limit: 20)
                .Select(result => result.BookId)
                .ToArray();

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void HybridRanking_DiversityPolicy_InterleavesKnownAuthorsDeterministically()
    {
        var service = new HybridRankingService();
        var exact = new[]
        {
            ExactWithAuthor("book-a", "Author A", 10),
            ExactWithAuthor("book-b", "Author A", 9),
            ExactWithAuthor("book-c", "Author A", 8),
            ExactWithAuthor("book-d", "Author B", 7),
        };

        IReadOnlyList<HybridRankedResult> results = service.Rank(
            exact,
            [],
            new Dictionary<string, HybridBookSignals>(StringComparer.Ordinal),
            HybridRankingWeights.Default,
            Now,
            limit: 2,
            diversityPolicy: new HybridDiversityPolicy(MaxResultsPerAuthor: 1));

        Assert.Equal(["book-a", "book-d"], results.Select(result => result.BookId).ToArray());
    }

    private static CombinedSearchResult Exact(string bookId, double score) =>
        new(bookId, Title: bookId, Author: "Author", Score: score, MatchedFields: [], FtsHits: []);

    private static CombinedSearchResult ExactWithAuthor(string bookId, string author, double score) =>
        new(bookId, Title: bookId, Author: author, Score: score, MatchedFields: [], FtsHits: []);

    private static SemanticSearchResult Semantic(string bookId, float score) =>
        new(bookId, Title: bookId, ChunkId: null, Source: null, Snippet: null, SemanticScore: score, ExactFallback: false);

    private static HybridBookSignals Signals(
        string bookId,
        int? daysAgo,
        ReadingStatus? status,
        int? rating) =>
        new(bookId, daysAgo.HasValue ? Now.AddDays(-daysAgo.Value) : null, status, rating);
}

