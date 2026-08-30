using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels;

namespace OgmaLibrary.App.Views;

/// <summary>The non-blocking desktop shell with startup recovery states.</summary>
public sealed partial class DesktopShellWindow : Window
{
    private Button? _retryButton;
    private StartupShellViewModel? _viewModel;

    /// <summary>Initializes the window.</summary>
    public DesktopShellWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _retryButton = this.FindControl<Button>("RetryButton");
        DataContextChanged += OnDataContextChanged;
    }

    private async void RetryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StartupShellViewModel viewModel)
        {
            await viewModel.RetryAsync().ConfigureAwait(true);
        }
    }

    private async void ExportDiagnosticsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StartupShellViewModel viewModel)
        {
            await viewModel.ExportDiagnosticsAsync().ConfigureAwait(true);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as StartupShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StartupShellViewModel.IsDegraded) or
                              nameof(StartupShellViewModel.CanRetry) &&
            _viewModel?.IsDegraded == true &&
            _viewModel.CanRetry)
        {
            // Let enabled/visibility bindings reach the control before moving
            // keyboard focus into the recovery surface.
            Dispatcher.UIThread.Post(() => _retryButton?.Focus(), DispatcherPriority.Input);
        }
    }
}
