using Avalonia.Controls;
using OgmaLibrary.App.ViewModels.Ai;

namespace OgmaLibrary.App.Views.Ai;

/// <summary>Recommendation panel view.</summary>
public sealed partial class RecommendationPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="RecommendationPanelView"/>.</summary>
    public RecommendationPanelView() => InitializeComponent();

    private RecommendationPanelViewModel? ViewModel => DataContext as RecommendationPanelViewModel;

    private async void Load_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private async void Ask_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private async void SubmitFeedback_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SubmitFeedbackAsync().ConfigureAwait(true);
        }
    }

    private async void OpenBook_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is Control { DataContext: RecommendationCardViewModel card })
        {
            await ViewModel.OpenBookAsync(card).ConfigureAwait(true);
        }
    }

    private async void OpenCitation_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is Control { DataContext: AnswerCitationViewModel citation })
        {
            await ViewModel.OpenCitationAsync(citation).ConfigureAwait(true);
        }
    }
}
