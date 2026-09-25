using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Classroom;

/// <summary>Code-behind for the classroom student smart-search view.</summary>
public partial class StudentSmartSearchView : UserControl
{
    public StudentSmartSearchView()
    {
        InitializeComponent();
    }

    private StudentSmartSearchViewModel? ViewModel => DataContext as StudentSmartSearchViewModel;

    private void PreviewButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PreviewButton_ClickAsync(sender, e), "classroom.preview_button_click");

    private async Task PreviewButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RequestPreviewAsync().ConfigureAwait(true);
        }
    }

    private void ConfirmSearchButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmSearchButton_ClickAsync(sender, e), "classroom.confirm_search_button_click");

    private async Task ConfirmSearchButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmSearchAsync().ConfigureAwait(true);
        }
    }

    private void CancelPreviewButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.CancelPreview();

    private void ClearAnswerButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.ClearAnswer();

    private void DeleteHistoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteHistoryButton_ClickAsync(sender, e), "classroom.delete_history_button_click");

    private async Task DeleteHistoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteHistoryAsync().ConfigureAwait(true);
        }
    }
}
