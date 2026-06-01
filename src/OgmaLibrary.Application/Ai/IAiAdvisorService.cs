using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>
/// Application use-case boundary for AI advisor features.
/// </summary>
public interface IAiAdvisorService
{
    /// <summary>Whether AI advisor features are currently enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets book recommendations for the current library context.</summary>
    Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
        RecommendationQuery query,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken);

    /// <summary>Gets a reading plan for the current library context.</summary>
    Task<ReadingPlan> GetReadingPlanAsync(
        ReadingPlanRequest request,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken);

    /// <summary>Gets an answer to a user question.</summary>
    Task<AnswerResponse> GetAnswerAsync(AnswerRequest request, CancellationToken cancellationToken);
}
