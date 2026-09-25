using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views;

/// <summary>The non-blocking desktop shell with startup recovery states.</summary>
public sealed partial class DesktopShellWindow : Window
{
    private Button? _retryButton;
    private TextBox? _commandPaletteBox;
    private ItemsControl? _toastHost;
    private StartupShellViewModel? _viewModel;

    /// <summary>Initializes the window.</summary>
    public DesktopShellWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _retryButton = this.FindControl<Button>("RetryButton");
        _commandPaletteBox = this.FindControl<TextBox>("CommandPaletteBox");
        _toastHost = this.FindControl<ItemsControl>("ToastHost");
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Binds the process notification surface (T02.7) independently of the window's DataContext.</summary>
    /// <param name="notifications">The notification centre.</param>
    public void AttachNotifications(NotificationCenterViewModel notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (_toastHost is not null)
        {
            _toastHost.DataContext = notifications;
        }
    }

    /// <summary>Restores and activates the window when a second launch hands off to this instance (K70).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();

        // Pulse Topmost so the window comes forward even when another app owns the foreground.
        Topmost = true;
        Topmost = false;
    }

    private void RetryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(RetryAsync, "startup.retry");

    private Task RetryAsync() =>
        DataContext is StartupShellViewModel viewModel ? viewModel.RetryAsync() : Task.CompletedTask;

    private void ExportDiagnosticsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(ExportDiagnosticsAsync, "startup.export_diagnostics");

    private Task ExportDiagnosticsAsync() =>
        DataContext is StartupShellViewModel viewModel
            ? viewModel.ExportDiagnosticsAsync()
            : Task.CompletedTask;

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

    private void CommandPaletteItem_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CommandPaletteItemAsync(sender), "shell.command_palette.execute");

    private Task CommandPaletteItemAsync(object? sender) =>
        sender is Button { DataContext: CommandPaletteItem item } &&
        DataContext is StartupShellViewModel { MainShell: { } shell }
            ? shell.ExecuteCommandAsync(item.Id)
            : Task.CompletedTask;

    private void CommandPaletteCloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StartupShellViewModel { MainShell: { } shell })
        {
            shell.CloseCommandPalette();
            e.Handled = true;
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
                 (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            shell.OpenCommandPalette();
            Dispatcher.UIThread.Post(() => _commandPaletteBox?.Focus());
            e.Handled = true;
        }
    }
}
