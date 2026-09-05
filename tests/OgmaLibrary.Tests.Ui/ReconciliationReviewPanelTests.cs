using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for the Phase 8 operator relocation-review workflow.</summary>
public sealed class ReconciliationReviewPanelTests
{
    [AvaloniaFact]
    public async Task Panel_LoadsCandidateAndExposesAccessibleDecisionControls()
    {
        var service = new RecordingReviewService();
        var viewModel = new ReconciliationReviewPanelViewModel(
            service,
            new InMemoryLocalizationService());

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        var view = new ReconciliationReviewPanelView { DataContext = viewModel };
        var window = new Window
        {
            Width = 520,
            Height = 500,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ReconciliationReviewItemViewModel item = Assert.Single(viewModel.Reviews);
        Assert.Null(item.SelectedPath);
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text =>
            text.Text == "Relocation reviews");
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button =>
            button.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) == "Accept path");
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button =>
            button.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) == "Reject");

        item.SelectedPath = "candidate-b.pdf";
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.CanAccept);
        await viewModel.AcceptSelectedAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((1L, ReconciliationReviewDecision.Accept, "candidate-b.pdf"), service.Decision);
        window.Close();
        viewModel.Dispose();
    }

    private sealed class RecordingReviewService : IReconciliationReviewService
    {
        private readonly ReconciliationReviewDescriptor _review = new(
            1,
            "01PH08ROOT0000000000000001",
            "01PH08OCCURRENCE0000000008",
            "ambiguous_relocation_review",
            ["candidate-a.pdf", "candidate-b.pdf"],
            DateTimeOffset.UtcNow);

        public (long, ReconciliationReviewDecision, string?)? Decision { get; private set; }

        public Task<IReadOnlyList<ReconciliationReviewDescriptor>> ListPendingAsync(
            string? libraryRootId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReconciliationReviewDescriptor>>([_review]);

        public Task DecideAsync(
            long reviewId,
            ReconciliationReviewDecision decision,
            string? selectedRelativePath = null,
            CancellationToken cancellationToken = default)
        {
            Decision = (reviewId, decision, selectedRelativePath);
            return Task.CompletedTask;
        }
    }
}
