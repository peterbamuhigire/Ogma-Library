using Avalonia.Controls;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Ai;

namespace OgmaLibrary.App.Views.Ai;

/// <summary>Recommendation panel view.</summary>
public sealed partial class RecommendationPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="RecommendationPanelView"/>.</summary>
    public RecommendationPanelView() => InitializeComponent();

    private RecommendationPanelViewModel? ViewModel => DataContext as RecommendationPanelViewModel;

    private void Load_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UiActions.Run(() => Load_ClickAsync(sender, e), "advisor.load_click");

    private async Task Load_ClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private void Ask_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UiActions.Run(() => Ask_ClickAsync(sender, e), "advisor.ask_click");

    private async Task Ask_ClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.AskAsync().ConfigureAwait(true);
        }
    }

    private void FeedbackRating_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            sender is Button { Tag: string tag } &&
            int.TryParse(tag, out int rating))
        {
            ViewModel.SetFeedbackRating(rating);
        }
    }

    private void SubmitFeedback_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UiActions.Run(() => SubmitFeedback_ClickAsync(sender, e), "advisor.submit_feedback_click");

    private async Task SubmitFeedback_ClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SubmitFeedbackAsync().ConfigureAwait(true);
        }
    }

    private void OpenBook_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UiActions.Run(() => OpenBook_ClickAsync(sender, e), "advisor.open_book_click");

    private async Task OpenBook_ClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is Control { DataContext: RecommendationCardViewModel card })
        {
            await ViewModel.OpenBookAsync(card).ConfigureAwait(true);
        }
    }

    private void OpenCitation_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UiActions.Run(() => OpenCitation_ClickAsync(sender, e), "advisor.open_citation_click");

    private async Task OpenCitation_ClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is Control { DataContext: AnswerCitationViewModel citation })
        {
            await ViewModel.OpenCitationAsync(citation).ConfigureAwait(true);
        }
    }
}
