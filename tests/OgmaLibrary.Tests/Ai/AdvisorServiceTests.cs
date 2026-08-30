using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 advisor service composition tests.</summary>
public sealed class AdvisorServiceTests
{
    [Fact]
    public async Task AdvisorService_DelegatesRecommendations()
    {
        RecommendationCard expected = Recommendation("BOOK-P13-SVC-001");
        var recommendations = new FakeRecommendationPipeline([expected]);
        AdvisorService service = CreateService(AiPrivacyTier.MetadataOnly, recommendations);

        IReadOnlyList<RecommendationCard> cards = await service.GetRecommendationsAsync(
            new RecommendationQuery("systems"),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
            CancellationToken.None);

        Assert.True(service.IsEnabled);
        Assert.Same(expected, Assert.Single(cards));
        Assert.Equal(1, recommendations.Calls);
    }

    [Fact]
    public async Task AdvisorService_DelegatesReadingPlan()
    {
        ReadingPlan expected = new(
            "learn systems",
            [new ReadingPlanStep(new BookId("BOOK-P13-SVC-001"), "Start here.", DifficultyLabel.Introductory, 3)],
            []);
        var readingPlans = new FakeReadingPlanPipeline(expected);
        AdvisorService service = CreateService(AiPrivacyTier.MetadataOnly, readingPlanPipeline: readingPlans);

        ReadingPlan plan = await service.GetReadingPlanAsync(
            new ReadingPlanRequest("learn systems"),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
            CancellationToken.None);

        Assert.Same(expected, plan);
        Assert.Equal(1, readingPlans.Calls);
    }

    [Fact]
    public async Task AdvisorDisabled_CatalogueBrowse_Unaffected()
    {
        AdvisorService service = CreateService(AiPrivacyTier.Offline);

        await Assert.ThrowsAsync<AiDisabledException>(() =>
            service.GetRecommendationsAsync(
                new RecommendationQuery("systems"),
                new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
                CancellationToken.None));
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public async Task GetAnswerAsync_ReturnsUnavailableScaffold_Before_V2()
    {
        AdvisorService service = CreateService(AiPrivacyTier.MetadataOnly);

        AnswerResponse response = await service.GetAnswerAsync(
            new AnswerRequest("What does this book say about systems?"),
            CancellationToken.None);

        Assert.False(response.IsV2);
        Assert.Empty(response.Citations);
        Assert.Contains("not configured", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static AdvisorService CreateService(
        AiPrivacyTier tier,
        IRecommendationPipeline? recommendationPipeline = null,
        IReadingPlanPipeline? readingPlanPipeline = null) =>
        new(
            new FakePrivacyService(tier),
            recommendationPipeline ?? new FakeRecommendationPipeline([]),
            readingPlanPipeline ?? new FakeReadingPlanPipeline(new ReadingPlan(
                "placeholder",
                [new ReadingPlanStep(new BookId("BOOK-P13-SVC-001"), "Placeholder.", DifficultyLabel.Introductory, null)],
                [])));

    private static RecommendationCard Recommendation(string bookId) =>
        new(
            new BookId(bookId),
            1,
            new ConfidenceScore(0.8),
            new RecommendationExplanation(
                "Matches the request.",
                [new ProvenanceItem(new BookId(bookId), RecommendationMatchField.Title, "Systems")],
                "gpt-test",
                AiPrivacyTier.MetadataOnly));

    private sealed class FakeRecommendationPipeline(IReadOnlyList<RecommendationCard> cards) : IRecommendationPipeline
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
            RecommendationQuery query,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(cards);
        }
    }

    private sealed class FakeReadingPlanPipeline(ReadingPlan plan) : IReadingPlanPipeline
    {
        public int Calls { get; private set; }

        public Task<ReadingPlan> GetReadingPlanAsync(
            ReadingPlanRequest request,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(plan);
        }
    }

    private sealed class FakePrivacyService(AiPrivacyTier tier) : IAiPrivacyService
    {
        public AiPrivacyTier GetActiveTier() => tier;

        public Task SetTierAsync(AiPrivacyTier tier, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordConsentAsync(AiConsentRecord consent, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> HasConsentAsync(
            AiPrivacyTier tier,
            string provider,
            string scope,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public AiPayloadPreview BuildPayloadPreview(AiRequest request) =>
            new(request.Tier, request.Provider, request.Model, request.QueryType, request.QueryText, request.MetadataFields, request.ContentChunks);
    }
}
