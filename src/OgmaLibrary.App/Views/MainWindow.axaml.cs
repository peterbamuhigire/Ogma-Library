using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OgmaLibrary.App.ViewModels;

namespace OgmaLibrary.App.Views;

/// <summary>The main application window (Phase 02 skeleton, wired for scanning in Phase 05).</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the main window and loads its XAML.</summary>
    public MainWindow() => AvaloniaXamlLoader.Load(this);

    private async void ChooseFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not null)
            {
                await vm.ChooseFolderAsync(topLevel).ConfigureAwait(true);
            }
        }
    }
}
