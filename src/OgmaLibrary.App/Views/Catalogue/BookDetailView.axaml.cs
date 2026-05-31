using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the book-detail slide-in panel (FR-CAT-004).</summary>
public partial class BookDetailView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="BookDetailView"/>.</summary>
    public BookDetailView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.Close();
        }
    }

    private void ReadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            _ = vm.OpenReaderAsync();
        }
    }

    private async void EnrichButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.EnrichMetadataAsync().ConfigureAwait(true);
        }
    }
}
