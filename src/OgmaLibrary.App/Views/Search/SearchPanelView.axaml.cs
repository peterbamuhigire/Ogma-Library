using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Search;

namespace OgmaLibrary.App.Views.Search;

/// <summary>
/// Code-behind for the Search destination. Keyboard map (Sept-23 Phase 13, task 13.7):
/// Down from the box moves into the results, Up on the first result returns to the box,
/// Enter opens the selected result at its page, Escape returns to the box.
/// </summary>
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

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is SearchViewModel vm)
        {
            UiActions.Run(() => vm.RefreshCoverageAsync(), "search.coverage_refresh");
        }
    }

    private void SearchPanel_KeyDown(object? sender, KeyEventArgs e) =>
        UiActions.Run(() => SearchPanel_KeyDownAsync(e), "search.search_panel_key_down");

    private async Task SearchPanel_KeyDownAsync(KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || DataContext is not SearchViewModel vm)
        {
            return;
        }

        e.Handled = true;
        await vm.OpenSelectedAsync().ConfigureAwait(true);
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || DataContext is not SearchViewModel vm || vm.Results.Count == 0)
        {
            return;
        }

        vm.SelectedResult ??= vm.Results[0];
        FocusSelectedResult();
        e.Handled = true;
    }

    private void SearchResults_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SearchViewModel vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                UiActions.Run(() => vm.OpenSelectedAsync(), "search.results_open");
                break;
            case Key.Escape:
                e.Handled = true;
                SearchBox.Focus();
                break;
            case Key.Up when SearchResults.SelectedIndex <= 0:
                e.Handled = true;
                SearchBox.Focus();
                break;
            default:
                break;
        }
    }

    private void SearchResults_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            UiActions.Run(() => vm.OpenSelectedAsync(), "search.results_double_tap");
        }
    }

    private void FocusSelectedResult()
    {
        int index = Math.Max(0, SearchResults.SelectedIndex);
        SearchResults.ScrollIntoView(index);
        if (SearchResults.ContainerFromIndex(index) is ListBoxItem item)
        {
            item.Focus(NavigationMethod.Directional);
        }
        else
        {
            SearchResults.Focus(NavigationMethod.Directional);
        }
    }

    private void Retry_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            UiActions.Run(() => vm.RetryAsync(), "search.retry_click");
        }
    }

    private void ReviewCoverage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            vm.ReviewCoverage();
        }
    }

    private void OpenSelected_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenSelected_ClickAsync(), "search.open_selected_click");

    private async Task OpenSelected_ClickAsync()
    {
        if (DataContext is SearchViewModel vm)
        {
            await vm.OpenSelectedAsync().ConfigureAwait(true);
        }
    }
}
