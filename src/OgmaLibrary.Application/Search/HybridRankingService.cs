using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Deterministic hybrid ranking for Phase 11 semantic search.
/// </summary>
public sealed class HybridRankingService : IHybridRankingService
{
    private const double RecencyHalfLifeDays = 30.0;
    private static readonly double RecencyLambda = Math.Log(2.0) / RecencyHalfLifeDays;

    /// <inheritdoc />
    public IReadOnlyList<HybridRankedResult> Rank(
        IReadOnlyList<CombinedSearchResult> exactResults,
        IReadOnlyList<SemanticSearchResult> semanticResults,
        IReadOnlyDictionary<string, HybridBookSignals> bookSignals,
        HybridRankingWeights weights,
        DateTimeOffset nowUtc,
        int limit,
        HybridDiversityPolicy? diversityPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(exactResults);
        ArgumentNullException.ThrowIfNull(semanticResults);
        ArgumentNullException.ThrowIfNull(bookSignals);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        HybridDiversityPolicy activeDiversity = diversityPolicy ?? HybridDiversityPolicy.None;
        if (activeDiversity.MaxResultsPerAuthor < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diversityPolicy),
                "Maximum results per author must be positive.");
        }

        HybridRankingWeights activeWeights = NormalizeWeights(
            weights,
            semanticResults.Any(result => result.SemanticScore.HasValue));
        double maxExactScore = exactResults.Count == 0
            ? 0.0
            : exactResults.Max(result => Math.Max(0.0, result.Score));

        Dictionary<string, CombinedSearchResult> exactByBook = exactResults
            .GroupBy(result => result.BookId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(result => result.Score)
                    .ThenBy(result => result.BookId, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);
        Dictionary<string, SemanticSearchResult> semanticByBook = semanticResults
            .GroupBy(result => result.BookId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(result => result.SemanticScore ?? double.NegativeInfinity)
                    .ThenBy(result => result.BookId, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);

        IEnumerable<string> bookIds = exactByBook.Keys
            .Concat(semanticByBook.Keys)
            .Concat(bookSignals.Keys)
            .Distinct(StringComparer.Ordinal);

        List<HybridRankedResult> ordered = bookIds
            .Select(bookId => RankBook(
                bookId,
                exactByBook.GetValueOrDefault(bookId),
                semanticByBook.GetValueOrDefault(bookId),
                bookSignals.GetValueOrDefault(bookId),
                activeWeights,
                maxExactScore,
                nowUtc))
            .OrderByDescending(result => result.HybridScore)
            .ThenBy(result => result.BookId, StringComparer.Ordinal)
            .ToList();

        if (activeDiversity == HybridDiversityPolicy.None)
        {
            return ordered.Take(limit).ToList();
        }

        var authorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<HybridRankedResult> diverse = [];
        foreach (HybridRankedResult result in ordered)
        {
            string authorKey = AuthorKey(result);
            authorCounts.TryGetValue(authorKey, out int count);
            if (count >= activeDiversity.MaxResultsPerAuthor)
            {
                continue;
            }

            diverse.Add(result);
            authorCounts[authorKey] = count + 1;
            if (diverse.Count == limit)
            {
                break;
            }
        }

        return diverse;
    }

    /// <summary>Computes the exponential-decay recency score.</summary>
    public static double RecencyScore(DateTimeOffset? lastOpenedUtc, DateTimeOffset nowUtc)
    {
        if (!lastOpenedUtc.HasValue)
        {
            return 0.0;
        }

        double days = Math.Max(0.0, (nowUtc - lastOpenedUtc.Value).TotalDays);
        return Math.Exp(-RecencyLambda * days);
    }

    /// <summary>Maps the available domain reading states onto Phase 11 status weights.</summary>
    public static double StatusScore(ReadingStatus? status) =>
        status switch
        {
            ReadingStatus.Reading => 1.0,
            ReadingStatus.Finished => 0.5,
            ReadingStatus.Unread => 0.0,
            ReadingStatus.Abandoned => 0.0,
            _ => 0.0,
        };

    /// <summary>Normalizes a 1-5 rating into the 0-1 rank signal range.</summary>
    public static double RatingScore(int? rating)
    {
        if (!rating.HasValue)
        {
            return 0.0;
        }

        return Math.Clamp(rating.Value, 0, 5) / 5.0;
    }

    private static HybridRankedResult RankBook(
        string bookId,
        CombinedSearchResult? exact,
        SemanticSearchResult? semantic,
        HybridBookSignals? signals,
        HybridRankingWeights weights,
        double maxExactScore,
        DateTimeOffset nowUtc)
    {
        double exactScore = NormalizeExactScore(exact?.Score, maxExactScore);
        double recencyScore = RecencyScore(signals?.LastOpenedUtc, nowUtc);
        double statusScore = StatusScore(signals?.ReadingStatus);
        double ratingScore = RatingScore(signals?.Rating);
        double? semanticScore = NormalizeSemanticScore(semantic?.SemanticScore);

        double hybridScore =
            (exactScore * weights.ExactWeight) +
            (recencyScore * weights.RecencyWeight) +
            (statusScore * weights.StatusWeight) +
            (ratingScore * weights.RatingWeight) +
            ((semanticScore ?? 0.0) * weights.SemanticWeight);

        return new HybridRankedResult(
            bookId,
            exact?.Title ?? semantic?.Title,
            exact?.Author,
            hybridScore,
            exactScore,
            recencyScore,
            statusScore,
            ratingScore,
            semanticScore,
            exact,
            semantic);
    }

    private static double NormalizeExactScore(double? score, double maxExactScore)
    {
        if (!score.HasValue || maxExactScore <= 0.0)
        {
            return 0.0;
        }

        return Math.Clamp(score.Value / maxExactScore, 0.0, 1.0);
    }

    private static double? NormalizeSemanticScore(float? score)
    {
        if (!score.HasValue)
        {
            return null;
        }

        double clamped = Math.Clamp(score.Value, -1.0f, 1.0f);
        return (clamped + 1.0) / 2.0;
    }

    private static HybridRankingWeights NormalizeWeights(
        HybridRankingWeights weights,
        bool semanticAvailable)
    {
        double exact = ValidWeight(weights.ExactWeight);
        double recency = ValidWeight(weights.RecencyWeight);
        double status = ValidWeight(weights.StatusWeight);
        double rating = ValidWeight(weights.RatingWeight);
        double semantic = ValidWeight(weights.SemanticWeight);

        if (!semanticAvailable)
        {
            exact += semantic;
            semantic = 0.0;
        }

        double total = exact + recency + status + rating + semantic;
        if (total <= 0.0)
        {
            return semanticAvailable
                ? HybridRankingWeights.Default
                : NormalizeWeights(HybridRankingWeights.Default, semanticAvailable: false);
        }

        return new HybridRankingWeights(
            exact / total,
            recency / total,
            status / total,
            rating / total,
            semantic / total);
    }

    private static double ValidWeight(double weight) =>
        double.IsFinite(weight) && weight > 0.0 ? weight : 0.0;

    private static string AuthorKey(HybridRankedResult result) =>
        string.IsNullOrWhiteSpace(result.Author) ? result.BookId : result.Author.Trim();
}

