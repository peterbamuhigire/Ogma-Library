using OgmaLibrary.Application.Extensions;

namespace OgmaLibrary.Application.Ai.Extensions;

/// <summary>
/// Source of candidate books for the recommendation pipeline. Phase 23's
/// Extension SDK will review this surface before publishing it to plugins.
/// </summary>
[ExtensionPoint]
internal interface IRecommendationSource
{
    /// <summary>Returns local candidate books for a recommendation query.</summary>
    Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken);
}
