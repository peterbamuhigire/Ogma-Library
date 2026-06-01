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

    private void EraseEmbeddings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.RequestEmbeddingErasureConfirmation();
        }
    }

    private async void ConfirmEmbeddingErasure_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            await vm.ConfirmEmbeddingErasureAsync().ConfigureAwait(true);
        }
    }

    private void CancelEmbeddingErasure_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.CancelEmbeddingErasureConfirmation();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.CancelRebuild();
        }
    }

    private async void PauseOcr_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.PauseOcrJobAsync(job).ConfigureAwait(true);
        }
    }

    private async void CancelOcr_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.CancelOcrJobAsync(job).ConfigureAwait(true);
        }
    }

    private async void RetryOcr_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.RetryOcrJobAsync(job).ConfigureAwait(true);
        }
    }
}
