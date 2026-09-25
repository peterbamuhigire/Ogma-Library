using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind event bridge for the relocation review panel.</summary>
public partial class ReconciliationReviewPanelView : UserControl
{
    /// <summary>Raised when the operator closes the panel.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Initializes the relocation review panel.</summary>
    public ReconciliationReviewPanelView() => InitializeComponent();

    private void Reload_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Reload_ClickAsync(sender, e), "catalogue.reload_click");

    private async Task Reload_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private void Accept_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Accept_ClickAsync(sender, e), "catalogue.accept_click");

    private async Task Accept_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.AcceptSelectedAsync().ConfigureAwait(true);
        }
    }

    private void Reject_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Reject_ClickAsync(sender, e), "catalogue.reject_click");

    private async Task Reject_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.RejectSelectedAsync().ConfigureAwait(true);
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
