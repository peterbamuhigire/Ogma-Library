using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private async void PreviewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RequestPreviewAsync().ConfigureAwait(true);
        }
    }

    private async void ConfirmSearchButton_Click(object? sender, RoutedEventArgs e)
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
}
