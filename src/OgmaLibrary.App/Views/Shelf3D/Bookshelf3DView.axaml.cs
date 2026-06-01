using Avalonia.Controls;
using OgmaLibrary.App.ViewModels.Shelf3D;

namespace OgmaLibrary.App.Views.Shelf3D;

/// <summary>View for the WebView-hosted 3D bookshelf and accessible fallback.</summary>
public partial class Bookshelf3DView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="Bookshelf3DView"/>.</summary>
    public Bookshelf3DView()
    {
        InitializeComponent();
    }

    private async void ShelfLayout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Bookshelf3DViewModel viewModel)
        {
            await viewModel.SetLayoutAsync("shelf").ConfigureAwait(false);
        }
    }

    private async void GridLayout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Bookshelf3DViewModel viewModel)
        {
            await viewModel.SetLayoutAsync("grid3d").ConfigureAwait(false);
        }
    }
}
