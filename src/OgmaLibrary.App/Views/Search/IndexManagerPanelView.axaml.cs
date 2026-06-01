using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Search;

namespace OgmaLibrary.App.Views.Search;

/// <summary>Code-behind for the Phase 10 Index Manager panel.</summary>
public partial class IndexManagerPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="IndexManagerPanelView"/>.</summary>
    public IndexManagerPanelView()
    {
        InitializeComponent();
    }

    private async void Rebuild_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.RequestRebuildConfirmation();
            await Task.CompletedTask.ConfigureAwait(true);
        }
    }

    private async void ConfirmRebuild_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            await vm.ConfirmRebuildAsync().ConfigureAwait(true);
        }
    }

    private void CancelConfirmation_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.CancelRebuildConfirmation();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.CancelRebuild();
        }
    }
}
