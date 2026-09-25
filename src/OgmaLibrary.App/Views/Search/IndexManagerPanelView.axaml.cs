using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
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

    private void Rebuild_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Rebuild_ClickAsync(sender, e), "search.rebuild_click");

    private async Task Rebuild_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm)
        {
            vm.RequestRebuildConfirmation();
            await Task.CompletedTask.ConfigureAwait(true);
        }
    }

    private void ConfirmRebuild_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmRebuild_ClickAsync(sender, e), "search.confirm_rebuild_click");

    private async Task ConfirmRebuild_ClickAsync(object? sender, RoutedEventArgs e)
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

    private void ConfirmEmbeddingErasure_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmEmbeddingErasure_ClickAsync(sender, e), "search.confirm_embedding_erasure_click");

    private async Task ConfirmEmbeddingErasure_ClickAsync(object? sender, RoutedEventArgs e)
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

    private void PauseOcr_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PauseOcr_ClickAsync(sender, e), "search.pause_ocr_click");

    private async Task PauseOcr_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.PauseOcrJobAsync(job).ConfigureAwait(true);
        }
    }

    private void CancelOcr_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CancelOcr_ClickAsync(sender, e), "search.cancel_ocr_click");

    private async Task CancelOcr_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.CancelOcrJobAsync(job).ConfigureAwait(true);
        }
    }

    private void RetryOcr_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RetryOcr_ClickAsync(sender, e), "search.retry_ocr_click");

    private async Task RetryOcr_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexManagerViewModel vm &&
            sender is Control { DataContext: OcrJobStatusDisplayItem job })
        {
            await vm.RetryOcrJobAsync(job).ConfigureAwait(true);
        }
    }
}
