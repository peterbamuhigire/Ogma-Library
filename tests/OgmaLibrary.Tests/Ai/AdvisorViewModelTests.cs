using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 AI advisor view-model tests.</summary>
public sealed class AdvisorViewModelTests
{
    [Fact]
    public async Task RecommendationPanel_LoadsCardsAndOpensBook()
    {
        RecommendationCard card = Recommendation("BOOK-P13-UI-001");
        var advisor = new FakeAdvisorService([card], null);
        var navigation = new RecordingNavigation();
        using var viewModel = new RecommendationPanelViewModel(advisor, navigation, new InMemoryLocalizationService())
        {
            Query = "systems",
        };

        await viewModel.LoadAsync();
        await viewModel.OpenBookAsync(viewModel.Recommendations[0]);

        RecommendationCardViewModel item = Assert.Single(viewModel.Recommendations);
        Assert.Equal("BOOK-P13-UI-001", item.BookId);
        Assert.Equal("High", item.ConfidenceText);
        Assert.Equal("BOOK-P13-UI-001", navigation.OpenedBookId);
    }

    [Fact]
    public async Task ReadingPlan_GeneratesStepsAndResolvesBookTitle()
    {
        ReadingPlan plan = new(
            "learn systems",
            [new ReadingPlanStep(new BookId("BOOK-P13-UI-001"), "Start with foundations.", DifficultyLabel.Introductory, 4)],
            [new Checkpoint(0, "Summarize the core idea.")]);
        var advisor = new FakeAdvisorService([], plan);
        using var viewModel = new ReadingPlanViewModel(
            advisor,
            new FakeCatalogueReadModel(),
            new RecordingNavigation(),
            new InMemoryLocalizationService())
        {
            Goal = "learn systems",
        };

        await viewModel.GenerateAsync();

        PlanStepViewModel step = Assert.Single(viewModel.Steps);
        Assert.Equal("Thinking in Systems", step.BookTitle);
        Assert.Equal("Introductory", step.DifficultyText);
        Assert.Single(viewModel.Checkpoints);
    }

    private static RecommendationCard Recommendation(string bookId) =>
        new(
            new BookId(bookId),
            1,
            new ConfidenceScore(0.82),
            new RecommendationExplanation(
                "Matches your goal.",
                [new ProvenanceItem(new BookId(bookId), RecommendationMatchField.Tags, "systems")],
                "gpt-test",
                AiPrivacyTier.MetadataOnly));

    private sealed class FakeAdvisorService(
        IReadOnlyList<RecommendationCard> recommendations,
        ReadingPlan? plan) : IAiAdvisorService
    {
        public bool IsEnabled => true;

        public Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
            RecommendationQuery query,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(recommendations);

        public Task<ReadingPlan> GetReadingPlanAsync(
            ReadingPlanRequest request,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(plan ?? throw new InvalidOperationException("No plan configured."));

        public Task<AnswerResponse> GetAnswerAsync(AnswerRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class RecordingNavigation : IBookDetailNavigationService
    {
        public string? OpenedBookId { get; private set; }

        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default)
        {
            OpenedBookId = bookId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCatalogueReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(new BookDetailProjection(
                bookId,
                "Thinking in Systems",
                ["Donella Meadows"],
                2008,
                null,
                null,
                null,
                0,
                null,
                null,
                null,
                null,
                null,
                0,
                [],
                null));

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
