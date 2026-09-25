using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
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

    private void OpenReferenceButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenReferenceButton_ClickAsync(sender, e), "reader.open_reference_button_click");

    private async Task OpenReferenceButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SplitViewViewModel vm)
        {
            await vm.OpenReferenceAsync().ConfigureAwait(true);
        }
    }
}
