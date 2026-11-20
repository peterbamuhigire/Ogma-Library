using Avalonia.Controls;
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

    private void PickerResults_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { DataContext: SplitPaneViewModel pane } list &&
            list.SelectedItem is BookPickerItem item)
        {
            list.SelectedItem = null;
            pane.Picker.Pick(item);
        }
    }
}
