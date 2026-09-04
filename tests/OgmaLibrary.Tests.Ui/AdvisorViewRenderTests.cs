using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.Views.Ai;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless render tests for Phase 13 advisor surfaces.</summary>
public sealed class AdvisorViewRenderTests
{
    [AvaloniaFact]
    public async Task AdvisorViews_RenderLoadedRecommendationAndPlan()
    {
        var localization = new InMemoryLocalizationService();
        var advisor = new FakeAdvisorService();
        var catalogue = new FakeCatalogueReadModel();
        var navigation = new RecordingNavigation();
        using var recommendations = new RecommendationPanelViewModel(advisor, navigation, localization)
        {
            Query = "systems",
        };
        using var plan = new ReadingPlanViewModel(advisor, catalogue, navigation, localization)
        {
            Goal = "learn systems",
        };

        await recommendations.LoadAsync();
        await recommendations.AskAsync();
        await plan.GenerateAsync();
        Dispatcher.UIThread.RunJobs();

        var content = new StackPanel
        {
            Children =
            {
                new RecommendationPanelView { DataContext = recommendations },
                new ReadingPlanView { DataContext = plan },
            },
        };
        var window = new Window
        {
            Width = 1000,
            Height = 800,
            Content = content,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame!.Size.Width > 100);
        Assert.True(frame.Size.Height > 100);
        Assert.Contains("BOOK-P13-UI-001", recommendations.Recommendations[0].BookId, StringComparison.Ordinal);
        Assert.Equal("Local answer from the library.", recommendations.AnswerText);
        Assert.Equal("Thinking in Systems", plan.Steps[0].BookTitle);
        window.Close();
    }

    private sealed class FakeAdvisorService : IAiAdvisorService
    {
        public bool IsEnabled => true;

        public Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
            RecommendationQuery query,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecommendationCard>>(
            [
                new RecommendationCard(
                    new BookId("BOOK-P13-UI-001"),
                    1,
                    new ConfidenceScore(0.82),
                    new RecommendationExplanation(
                        "Matches your goal.",
                        [new ProvenanceItem(new BookId("BOOK-P13-UI-001"), RecommendationMatchField.Tags, "systems")],
                        "gpt-test",
                        AiPrivacyTier.MetadataOnly)),
            ]);

        public Task<ReadingPlan> GetReadingPlanAsync(
            ReadingPlanRequest request,
            RecommendationGenerationOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReadingPlan(
                "learn systems",
                [new ReadingPlanStep(new BookId("BOOK-P13-UI-001"), "Start with foundations.", DifficultyLabel.Introductory, 4)],
                [new Checkpoint(0, "Summarize the core idea.")]));

        public Task<AnswerResponse> GetAnswerAsync(AnswerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AnswerResponse("Local answer from the library.", [], IsV2: true));
    }

    private sealed class RecordingNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
