using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views;

/// <summary>The non-blocking desktop shell with startup recovery states.</summary>
public sealed partial class DesktopShellWindow : Window
{
    private Button? _retryButton;
    private TextBox? _commandPaletteBox;
    private ItemsControl? _commandPaletteList;
    private Button? _shortcutsCloseButton;
    private IInputElement? _focusBeforeOverlay;
    private MainShellViewModel? _mainShell;
    private ItemsControl? _toastHost;
    private StartupShellViewModel? _viewModel;

    /// <summary>Initializes the window.</summary>
    public DesktopShellWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _retryButton = this.FindControl<Button>("RetryButton");
        _commandPaletteBox = this.FindControl<TextBox>("CommandPaletteBox");
        _commandPaletteList = this.FindControl<ItemsControl>("CommandPaletteList");
        _shortcutsCloseButton = this.FindControl<Button>("ShortcutsCloseButton");
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

        if (_mainShell is not null)
        {
            _mainShell.PropertyChanged -= OnMainShellPropertyChanged;
        }

        _mainShell = _viewModel?.MainShell;
        if (_mainShell is not null)
        {
            _mainShell.PropertyChanged += OnMainShellPropertyChanged;
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
            ? shell.ExecuteCommandAsync(item.Id, this)
            : Task.CompletedTask;

    private void CommandPaletteCloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StartupShellViewModel { MainShell: { } shell })
        {
            shell.CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void CommandPaletteScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is StartupShellViewModel { MainShell: { } shell })
        {
            shell.CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void ShortcutScrim_PointerPressed(object? sender, PointerPressedEventArgs e) => CloseShortcuts(e);

    private void ShortcutsClose_Click(object? sender, RoutedEventArgs e) => CloseShortcuts(e);

    private void CloseShortcuts(RoutedEventArgs e)
    {
        if (DataContext is StartupShellViewModel { MainShell: { } shell })
        {
            shell.IsShortcutSheetOpen = false;
            e.Handled = true;
        }
    }

    private void CommandPaletteBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not StartupShellViewModel { MainShell: { } shell })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                shell.CloseCommandPalette();
                e.Handled = true;
                break;
            case Key.Enter when shell.CommandPaletteItems.Count > 0:
                string id = shell.CommandPaletteItems[0].Id;
                UiActions.Run(() => shell.ExecuteCommandAsync(id, this), "shell.command_palette.execute");
                e.Handled = true;
                break;
            case Key.Down:
                PaletteButtons().FirstOrDefault()?.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not StartupShellViewModel { MainShell: { } shell })
        {
            return;
        }

        List<Button> buttons = PaletteButtons().ToList();
        int index = buttons.FindIndex(button => button.IsFocused);
        switch (e.Key)
        {
            case Key.Escape:
                shell.CloseCommandPalette();
                e.Handled = true;
                break;
            case Key.Down when index >= 0 && index < buttons.Count - 1:
                buttons[index + 1].Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.Up when index > 0:
                buttons[index - 1].Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.Up when index == 0:
                _commandPaletteBox?.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
        }
    }

    private IEnumerable<Button> PaletteButtons() =>
        _commandPaletteList?.GetVisualDescendants().OfType<Button>() ?? [];

    private void OnMainShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainShellViewModel shell)
        {
            return;
        }

        if (e.PropertyName == nameof(MainShellViewModel.IsCommandPaletteOpen))
        {
            if (shell.IsCommandPaletteOpen)
            {
                _focusBeforeOverlay ??= FocusManager?.GetFocusedElement();
                Dispatcher.UIThread.Post(() => _commandPaletteBox?.Focus(), DispatcherPriority.Input);
            }
            else
            {
                RestoreFocus();
            }
        }
        else if (e.PropertyName == nameof(MainShellViewModel.IsShortcutSheetOpen))
        {
            if (shell.IsShortcutSheetOpen)
            {
                _focusBeforeOverlay ??= FocusManager?.GetFocusedElement();
                Dispatcher.UIThread.Post(() => _shortcutsCloseButton?.Focus(), DispatcherPriority.Input);
            }
            else
            {
                RestoreFocus();
            }
        }
    }

    private void RestoreFocus()
    {
        IInputElement? target = _focusBeforeOverlay;
        _focusBeforeOverlay = null;
        if (target is Control { IsEffectivelyVisible: true } control)
        {
            Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Input);
        }
    }

    private void DesktopShellWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not StartupShellViewModel { MainShell: { } shell })
        {
            return;
        }

        if (e.Key == Key.Escape && shell.IsCommandPaletteOpen)
        {
            shell.CloseCommandPalette();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && shell.IsShortcutSheetOpen)
        {
            shell.IsShortcutSheetOpen = false;
            e.Handled = true;
        }
        else if (shell.TryExecuteGesture(e.Key, e.KeyModifiers, this))
        {
            // Gestures pressed outside the library surface (for example with the palette open).
            e.Handled = true;
        }
    }
}
