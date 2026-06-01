using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Blends exact, semantic, and reader-history signals into a deterministic
/// book-level ranking.
/// </summary>
public interface IHybridRankingService
{
    /// <summary>
    /// Ranks the union of exact and semantic results using the supplied weights
    /// and per-book reader signals.
    /// </summary>
    IReadOnlyList<HybridRankedResult> Rank(
        IReadOnlyList<CombinedSearchResult> exactResults,
        IReadOnlyList<SemanticSearchResult> semanticResults,
        IReadOnlyDictionary<string, HybridBookSignals> bookSignals,
        HybridRankingWeights weights,
        DateTimeOffset nowUtc,
        int limit);
}

/// <summary>Hybrid ranking weights before active-weight normalization.</summary>
public sealed record HybridRankingWeights(
    double ExactWeight,
    double RecencyWeight,
    double StatusWeight,
    double RatingWeight,
    double SemanticWeight)
{
    /// <summary>Phase 11 default weighting from FR-SEARCH-005.</summary>
    public static HybridRankingWeights Default { get; } = new(0.35, 0.10, 0.10, 0.10, 0.35);
}

/// <summary>Reader and metadata signals used by hybrid ranking.</summary>
public sealed record HybridBookSignals(
    string BookId,
    DateTimeOffset? LastOpenedUtc,
    ReadingStatus? ReadingStatus,
    int? Rating);

/// <summary>One deterministic hybrid-ranked book result.</summary>
public sealed record HybridRankedResult(
    string BookId,
    string? Title,
    string? Author,
    double HybridScore,
    double ExactScore,
    double RecencyScore,
    double StatusScore,
    double RatingScore,
    double? SemanticScore,
    CombinedSearchResult? ExactResult,
    SemanticSearchResult? SemanticResult);

