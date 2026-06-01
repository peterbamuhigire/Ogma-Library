namespace OgmaLibrary.Application.Ai;

/// <summary>
/// Application use-case boundary for AI advisor features.
/// </summary>
public interface IAiAdvisorService
{
    /// <summary>Whether AI advisor features are currently enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets book recommendations for the current library context.</summary>
    Task<AiCompletion> GetRecommendationsAsync(AiRequest request, CancellationToken cancellationToken);

    /// <summary>Gets a reading plan for the current library context.</summary>
    Task<AiCompletion> GetReadingPlanAsync(AiRequest request, CancellationToken cancellationToken);

    /// <summary>Gets an answer to a user question.</summary>
    Task<AiCompletion> GetAnswerAsync(AiRequest request, CancellationToken cancellationToken);
}
