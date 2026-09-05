using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Default application service for AI advisor use cases.</summary>
public sealed class AdvisorService : IAiAdvisorService
{
    private readonly IAiPrivacyService _privacyService;
    private readonly IRecommendationPipeline _recommendationPipeline;
    private readonly IReadingPlanPipeline _readingPlanPipeline;
    private readonly IAnswerPipeline _answerPipeline;

    /// <summary>Initializes a new instance of <see cref="AdvisorService"/>.</summary>
    public AdvisorService(
        IAiPrivacyService privacyService,
        IRecommendationPipeline recommendationPipeline,
        IReadingPlanPipeline readingPlanPipeline,
        IAnswerPipeline answerPipeline)
    {
        ArgumentNullException.ThrowIfNull(privacyService);
        ArgumentNullException.ThrowIfNull(recommendationPipeline);
        ArgumentNullException.ThrowIfNull(readingPlanPipeline);
        ArgumentNullException.ThrowIfNull(answerPipeline);

        _privacyService = privacyService;
        _recommendationPipeline = recommendationPipeline;
        _readingPlanPipeline = readingPlanPipeline;
        _answerPipeline = answerPipeline;
    }

    /// <inheritdoc />
    public bool IsEnabled => _privacyService.GetActiveTier() != AiPrivacyTier.Offline;

    /// <inheritdoc />
    public Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
        RecommendationQuery query,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken)
    {
        ThrowIfDisabled();
        return _recommendationPipeline.GetRecommendationsAsync(query, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReadingPlan> GetReadingPlanAsync(
        ReadingPlanRequest request,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken)
    {
        ThrowIfDisabled();
        return _readingPlanPipeline.GetReadingPlanAsync(request, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AnswerResponse> GetAnswerAsync(AnswerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _answerPipeline.GetAnswerAsync(request, cancellationToken);
    }

    private void ThrowIfDisabled()
    {
        if (!IsEnabled)
        {
            throw new AiDisabledException();
        }
    }

}
