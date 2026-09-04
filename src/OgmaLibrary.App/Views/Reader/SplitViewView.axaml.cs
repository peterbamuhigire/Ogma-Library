using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>Code-behind for the two-session split-view reader.</summary>
public partial class SplitViewView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="SplitViewView"/>.</summary>
    public SplitViewView()
    {
        InitializeComponent();
    }

    private async void OpenReferenceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SplitViewViewModel vm)
        {
            await vm.OpenReferenceAsync().ConfigureAwait(true);
        }
    }
}
