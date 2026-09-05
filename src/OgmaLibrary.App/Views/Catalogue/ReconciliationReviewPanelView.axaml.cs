using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind event bridge for the relocation review panel.</summary>
public partial class ReconciliationReviewPanelView : UserControl
{
    /// <summary>Raised when the operator closes the panel.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Initializes the relocation review panel.</summary>
    public ReconciliationReviewPanelView() => InitializeComponent();

    private async void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private async void Accept_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.AcceptSelectedAsync().ConfigureAwait(true);
        }
    }

    private async void Reject_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReconciliationReviewPanelViewModel viewModel)
        {
            await viewModel.RejectSelectedAsync().ConfigureAwait(true);
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
