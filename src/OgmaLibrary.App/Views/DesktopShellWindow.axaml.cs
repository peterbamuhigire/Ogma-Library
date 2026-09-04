using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views;

/// <summary>The non-blocking desktop shell with startup recovery states.</summary>
public sealed partial class DesktopShellWindow : Window
{
    private Button? _retryButton;
    private TextBox? _commandPaletteBox;
    private StartupShellViewModel? _viewModel;

    /// <summary>Initializes the window.</summary>
    public DesktopShellWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _retryButton = this.FindControl<Button>("RetryButton");
        _commandPaletteBox = this.FindControl<TextBox>("CommandPaletteBox");
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

    private async void CommandPaletteItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CommandPaletteItem item } &&
            DataContext is StartupShellViewModel { MainShell: { } shell })
        {
            await shell.ExecuteCommandAsync(item.Id).ConfigureAwait(true);
        }
    }

    private void CommandPaletteBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not StartupShellViewModel { MainShell: { } shell })
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            shell.CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void DesktopShellWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not StartupShellViewModel { MainShell: { } shell })
        {
            return;
        }

        if (e.Key == Key.Escape && shell.IsCommandPaletteOpen)
        {
            shell.CloseCommandPalette();
            e.Handled = true;
        }
        else if (e.Key == Key.P &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            shell.OpenCommandPalette();
            Dispatcher.UIThread.Post(() => _commandPaletteBox?.Focus());
            e.Handled = true;
        }
    }
}
