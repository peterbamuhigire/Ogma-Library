using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using static OgmaLibrary.Tests.Ui.Phase11ReviewFakes;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 4 (task 11.6): the Activity review card accepts proposals through the
/// undoable bulk-review service and explains partial failures and undo conflicts.
/// </summary>
public sealed class Phase11ProposalReviewTests
{
    [AvaloniaFact]
    public async Task AcceptAll_ThenUndo_RestoresThePriorValues()
    {
        (BookDetailViewModel vm, MutableReadModel reads, FakeReviewService review, FakeBulkService bulk, InMemoryLocalizationService loc) = await CreateAsync();
        Assert.Equal(2, vm.SuggestedChangeCount);

        await vm.AcceptAllProposalsAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([1L, 2L], Assert.Single(bulk.Previewed));
        Assert.Equal("local-user", bulk.LastActorId);
        Assert.Equal("Better Title", vm.Book!.Title);
        Assert.Equal(1999, vm.Book.Year);
        Assert.Equal(0, vm.SuggestedChangeCount);
        Assert.True(vm.CanUndoReview);
        Assert.Empty(review.Decisions);

        await vm.UndoReviewAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Original Title", vm.Book!.Title);
        Assert.Equal(2001, vm.Book.Year);
        Assert.Equal(2, vm.SuggestedChangeCount);
        Assert.False(vm.CanUndoReview);
        Assert.Equal(loc["Catalogue.BookDetail.Review.Undone"], vm.MetadataReviewStatusText);
    }

    [AvaloniaFact]
    public async Task SingleAccept_GoesThroughTheBulkService_AndIsUndoable()
    {
        (BookDetailViewModel vm, _, FakeReviewService review, FakeBulkService bulk, _) = await CreateAsync();

        await vm.AcceptMetadataProposalAsync(vm.PendingMetadataProposals.Single(p => p.FieldName == "Year"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([2L], Assert.Single(bulk.Previewed));
        Assert.Empty(review.Decisions);
        Assert.Equal(1999, vm.Book!.Year);
        Assert.Equal(1, vm.SuggestedChangeCount);
        Assert.True(vm.CanUndoReview);
    }

    [AvaloniaFact]
    public async Task Reject_StaysOnTheSingleDecisionService()
    {
        (BookDetailViewModel vm, _, FakeReviewService review, FakeBulkService bulk, _) = await CreateAsync();

        await vm.RejectMetadataProposalAsync(vm.PendingMetadataProposals[0]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([(1L, false)], review.Decisions);
        Assert.Empty(bulk.Previewed);
        Assert.False(vm.CanUndoReview);
    }

    [AvaloniaFact]
    public async Task PartialFailure_IsReportedPerField_WithoutRawErrors()
    {
        (BookDetailViewModel vm, _, _, FakeBulkService bulk, InMemoryLocalizationService loc) = await CreateAsync();
        bulk.FailFields.Add("Year");

        await vm.AcceptAllProposalsAsync();
        Dispatcher.UIThread.RunJobs();

        string status = vm.MetadataReviewStatusText!;
        Assert.Contains(loc["Catalogue.BookDetail.Edit.Label.Year"], status, StringComparison.Ordinal);
        Assert.DoesNotContain(loc["Catalogue.BookDetail.Edit.Label.Title"], status, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW-SECRET", status, StringComparison.Ordinal);
        Assert.Equal("Better Title", vm.Book!.Title);
        Assert.Equal(1, vm.SuggestedChangeCount);
        Assert.True(vm.CanUndoReview);
    }

    [AvaloniaFact]
    public async Task UndoConflict_ShowsALocalisedExplanation()
    {
        (BookDetailViewModel vm, _, _, FakeBulkService bulk, InMemoryLocalizationService loc) = await CreateAsync();
        await vm.AcceptAllProposalsAsync();
        bulk.UndoConflict = true;

        await vm.UndoReviewAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(loc["Catalogue.BookDetail.Review.UndoConflict"], vm.MetadataReviewStatusText);
        Assert.Equal("Better Title", vm.Book!.Title);
        Assert.False(vm.CanUndoReview);
    }

    [AvaloniaFact]
    public async Task ReviewCard_RendersItsCount_AndNamedButtons()
    {
        (BookDetailViewModel vm, _, _, _, InMemoryLocalizationService loc) = await CreateAsync();
        vm.IsVisible = true;
        vm.SelectedSection = InspectorSection.Activity;
        var view = new BookDetailView { DataContext = vm, Width = 360 };
        var window = new Window { Width = 360, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control card = FindById<Control>(view, "Detail.Review.Card");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal("2 suggested changes", FindById<TextBlock>(view, "Detail.Review.Title").Text);
        Assert.Equal("2 suggested changes", AutomationProperties.GetName(card));

        TextBlock badge = FindById<TextBlock>(view, "Detail.Section.Activity.Badge");
        Assert.True(badge.IsEffectivelyVisible);
        Assert.Equal("2", badge.Text);
        Button activity = FindById<Button>(view, "Detail.Section.Activity");
        Assert.Equal("Activity, 2 suggested changes", AutomationProperties.GetName(activity));

        Button acceptAll = FindById<Button>(view, "Detail.Review.AcceptAll");
        Assert.Equal(loc["Catalogue.BookDetail.Review.AcceptAll"], AutomationProperties.GetName(acceptAll));
        Assert.True(acceptAll.IsEnabled);
        Assert.Equal("Accept the suggested Title", AutomationProperties.GetName(FindById<Button>(view, "Detail.Review.Accept.Title")));
        Assert.Equal("Reject the suggested Year", AutomationProperties.GetName(FindById<Button>(view, "Detail.Review.Reject.Year")));
        Assert.False(FindById<Button>(view, "Detail.Review.Undo").IsEffectivelyVisible);

        acceptAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (int i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        Assert.True(FindById<Button>(view, "Detail.Review.Undo").IsEffectivelyVisible);
        Assert.False(badge.IsVisible);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NoMetadata_OffersEnrich_OrPointsToSettings(bool canEnrich)
    {
        var reads = new MutableReadModel(Projection() with
        {
            Authors = [],
            Year = null,
            Isbn = null,
            Doi = null,
            MetadataFields = [],
        });
        var loc = new InMemoryLocalizationService();
        var vm = new BookDetailViewModel(
            reads,
            new NoOpReaderNavigation(),
            loc,
            metadataEnrichment: canEnrich
                ? Fake<OgmaLibrary.Application.Metadata.IBookMetadataEnrichmentService>(new HashSet<string>())
                : null);
        await vm.LoadBookAsync(reads.Detail.BookId);
        vm.IsVisible = true;
        var view = new BookDetailView { DataContext = vm, Width = 360 };
        var window = new Window { Width = 360, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(FindById<Control>(view, "Detail.State.NoMetadata").IsEffectivelyVisible);
        Button enrich = FindById<Button>(view, "Detail.State.NoMetadata.Enrich");
        TextBlock hint = FindById<TextBlock>(view, "Detail.State.NoMetadata.Hint");
        Assert.Equal(canEnrich, enrich.IsEffectivelyVisible);
        Assert.Equal(!canEnrich, hint.IsEffectivelyVisible);
        Assert.Equal(loc["Catalogue.BookDetail.NoMetadata.SettingsHint"], hint.Text);
        window.Close();
    }

    private static T FindById<T>(BookDetailView view, string id)
        where T : Control =>
        Assert.Single(view.GetVisualDescendants().OfType<T>(), c => AutomationProperties.GetAutomationId(c) == id);

    private static async Task<(BookDetailViewModel, MutableReadModel, FakeReviewService, FakeBulkService, InMemoryLocalizationService)> CreateAsync()
    {
        var reads = new MutableReadModel(Projection());
        var review = new FakeReviewService(
        [
            Proposal(1, "Title", "Original Title", "Better Title"),
            Proposal(2, "Year", "2001", "1999"),
        ]);
        var bulk = new FakeBulkService(reads, review);
        var loc = new InMemoryLocalizationService();
        var vm = new BookDetailViewModel(
            reads,
            new NoOpReaderNavigation(),
            loc,
            metadataReviewService: review,
            metadataBulkReviewService: bulk);
        await vm.LoadBookAsync(reads.Detail.BookId);
        Dispatcher.UIThread.RunJobs();
        return (vm, reads, review, bulk, loc);
    }
}
