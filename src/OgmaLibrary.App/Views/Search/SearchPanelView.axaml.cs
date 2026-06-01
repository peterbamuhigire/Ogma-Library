using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Search;

namespace OgmaLibrary.App.Views.Search;

/// <summary>Code-behind for the Phase 10 search panel.</summary>
public partial class SearchPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="SearchPanelView"/>.</summary>
    public SearchPanelView()
    {
        InitializeComponent();
    }

    /// <summary>Moves keyboard focus into the search box.</summary>
    public void FocusSearchBox() =>
        Dispatcher.UIThread.Post(() => SearchBox.Focus());

    private async void SearchPanel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SearchViewModel vm)
        {
            return;
        }

        await vm.OpenSelectedAsync().ConfigureAwait(true);
        e.Handled = true;
    }

    private async void OpenSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            await vm.OpenSelectedAsync().ConfigureAwait(true);
        }
    }
}
