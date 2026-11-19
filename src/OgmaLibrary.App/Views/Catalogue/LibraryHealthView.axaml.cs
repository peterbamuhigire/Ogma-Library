using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the library health dashboard (Sept-23 Phase 10, META-008).</summary>
public partial class LibraryHealthView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="LibraryHealthView"/>.</summary>
    public LibraryHealthView()
    {
        InitializeComponent();
    }

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm &&
            sender is Control { DataContext: LibraryHealthTileViewModel tile })
        {
            vm.ActivateTile(tile.Kind);
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm)
        {
            vm.ReturnToCatalogue();
        }
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm)
        {
            UiActions.Run(() => vm.LoadAsync(), "health.refresh_click");
        }
    }

    private void Retry_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm &&
            sender is Control { DataContext: LibraryHealthFailedJobRow row })
        {
            UiActions.Run(() => vm.RetryJobAsync(row.JobId), "health.retry_click");
        }
    }

    private void Pause_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm &&
            sender is Control { DataContext: LibraryHealthBatchRow row })
        {
            UiActions.Run(() => vm.PauseBatchAsync(row.BatchId), "health.pause_click");
        }
    }

    private void Resume_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryHealthViewModel vm &&
            sender is Control { DataContext: LibraryHealthBatchRow row })
        {
            UiActions.Run(() => vm.ResumeBatchAsync(row.BatchId), "health.resume_click");
        }
    }
}
