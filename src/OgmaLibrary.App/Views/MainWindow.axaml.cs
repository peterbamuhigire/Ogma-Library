using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OgmaLibrary.App.Views;

/// <summary>The main application window (Phase 02 skeleton).</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the main window and loads its XAML.</summary>
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
