using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 30 offline quality metrics and interpreted-intent UX proofs.</summary>
public sealed class Phase30AdvisorQualityTests
{
    [Fact]
    public void OfflineEvaluator_ReportsRetrievalAndTrustMetrics()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P30-001", "Systems Foundations", ["systems"], "A practical systems introduction.", "A"),
            Candidate("BOOK-P30-002", "Unrelated Novel", ["fiction"], "A story.", "B"),
        ];
        AdvisorEvaluationCase evaluationCase = new(
            "phase30-systems",
            "Something about systems",
            candidates,
            new HashSet<string>(["BOOK-P30-001"], StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            k: 2);

        AdvisorEvaluationReport report = AdvisorOfflineEvaluator.Evaluate([evaluationCase]);

        Assert.Equal("advisor-evaluation-v1", report.EvaluationVersion);
        Assert.Equal(1, report.CaseCount);
        Assert.Equal(0.5, report.PrecisionAtK);
        Assert.Equal(1.0, report.RecallAtK);
        Assert.Equal(1.0, report.MeanReciprocalRank);
        Assert.Equal(1.0, report.NdcgAtK);
        Assert.Equal(1.0, report.GroundingRate);
        Assert.Equal(1.0, report.ConstraintSatisfactionRate);
        Assert.Equal(1.0, report.DiversityRate);
    }

    [Fact]
    public void OfflineEvaluatorGate_PassesOnlyAgainstExplicitThresholds()
    {
        AdvisorEvaluationCase evaluationCase = new(
            "phase30-gate",
            "systems",
            [Candidate("BOOK-P30-001", "Systems Foundations", ["systems"], "A practical systems introduction.", "A")],
            new HashSet<string>(["BOOK-P30-001"], StringComparer.Ordinal),
            k: 1);
        AdvisorEvaluationThresholds thresholds = new(1, 1, 1, 1, 1, 1, 1);

        AdvisorEvaluationGateResult result = AdvisorOfflineEvaluator.EvaluateGate([evaluationCase], thresholds);

        Assert.True(result.Passed);
        Assert.Empty(result.FailedMetrics);
    }

    [Fact]
    public void OfflineEvaluatorGate_FailsClosedForEmptyOrInsufficientEvidence()
    {
        AdvisorEvaluationGateResult result = AdvisorOfflineEvaluator.EvaluateGate(
            [],
            new AdvisorEvaluationThresholds(0, 0, 0, 0, 0, 0, 0));

        Assert.False(result.Passed);
        Assert.Contains("case-count", result.FailedMetrics);
    }

    [Fact]
    public void RecommendationPanel_ExposesInterpretedIntentWithoutNumericAiConfidence()
    {
        using RecommendationPanelViewModel viewModel = new(
            new NoOpAdvisor(),
            new NoOpNavigation(),
            new InMemoryLocalizationService())
        {
            Query = "Something on AI, but not a programming textbook.",
        };

        Assert.True(viewModel.HasInterpretedIntent);
        Assert.Contains("Topics: ai", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avoids: programming", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecommendationPanel_EditingQueryRecomputesInterpretedIntent()
    {
        using RecommendationPanelViewModel viewModel = new(
            new NoOpAdvisor(),
            new NoOpNavigation(),
            new InMemoryLocalizationService());

        viewModel.Query = "Something on AI, but not a programming textbook.";
        Assert.Contains("Topics: ai", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);

        viewModel.Query = "A short history book for beginners.";

        Assert.DoesNotContain("Topics: ai", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Length: ShortBook", viewModel.InterpretedIntentText, StringComparison.Ordinal);
        Assert.Contains("Level: Introductory", viewModel.InterpretedIntentText, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendationPanel_InterpretedIntent_UsesLocalizedLabels()
    {
        var localization = new InMemoryLocalizationService();
        using RecommendationPanelViewModel viewModel = new(
            new NoOpAdvisor(),
            new NoOpNavigation(),
            localization)
        {
            Query = "Something on AI, but not a programming textbook.",
        };

        localization.SetCulture("fr");
        Assert.Contains("Sujets : ai", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exclut : programming", viewModel.InterpretedIntentText, StringComparison.OrdinalIgnoreCase);

        localization.SetCulture("qps-ploc");
        Assert.StartsWith("[!!", viewModel.InterpretedIntentText, StringComparison.Ordinal);
    }

    private static BookMetadataDto Candidate(string id, string title, IReadOnlyList<string> tags, string description, string author) =>
        new(id, title, [author], tags, ["Education"], description, null, 2026, [], null);

    private sealed class NoOpAdvisor : IAiAdvisorService
    {
        public bool IsEnabled => true;

        public Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(RecommendationQuery query, RecommendationGenerationOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecommendationCard>>([]);

        public Task<ReadingPlan> GetReadingPlanAsync(ReadingPlanRequest request, RecommendationGenerationOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AnswerResponse> GetAnswerAsync(AnswerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
