using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Settings;

/// <summary>Code-behind for the Phase 16 Sharing settings surface.</summary>
public partial class SharingSettingsView : UserControl
{
    public SharingSettingsView()
    {
        InitializeComponent();
    }

    private HostSharingViewModel? ViewModel => DataContext as HostSharingViewModel;

    private void StartButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.RequestStartConfirmation();

    private async void ConfirmStartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmStartAsync().ConfigureAwait(true);
        }
    }

    private void CancelStartButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.CancelStartConfirmation();

    private async void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.StopAsync().ConfigureAwait(true);
        }
    }

    private async void ContentModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RequestContentModeChangeAsync().ConfigureAwait(true);
        }
    }

    private async void ConfirmFileStreamButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmFileStreamAsync().ConfigureAwait(true);
        }
    }

    private void CancelFileStreamButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.CancelFileStreamConfirmation();

    private async void ConnectToHostButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConnectToHostAsync().ConfigureAwait(true);
        }
    }

    private async void DiscoverHostsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DiscoverHostsAsync().ConfigureAwait(true);
        }
    }

    private async void SyncNowButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SyncNowAsync().ConfigureAwait(true);
        }
    }

    private async void SyncSettingsCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SaveSyncSettingsAsync().ConfigureAwait(true);
        }
    }

    private async void KeepLocalConflictButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.KeepLocalAnnotationConflictAsync().ConfigureAwait(true);
        }
    }

    private async void KeepServerConflictButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.KeepServerAnnotationConflictAsync().ConfigureAwait(true);
        }
    }

    private async void CopyJoinLinkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            await CopyTextAsync(sender, ViewModel.ManualJoinUri).ConfigureAwait(true))
        {
            ViewModel.MarkJoinLinkCopied();
        }
    }

    private async void CopyFingerprintButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            await CopyTextAsync(sender, ViewModel.FullFingerprintText).ConfigureAwait(true))
        {
            ViewModel.MarkFingerprintCopied();
        }
    }

    private async Task<bool> CopyTextAsync(object? sender, string text)
    {
        if (ViewModel is null)
        {
            return false;
        }

        var topLevel = ResolveTopLevel(sender);
        if (topLevel?.Clipboard is null)
        {
            ViewModel.ReportClipboardUnavailable();
            return false;
        }

        await topLevel.Clipboard.SetTextAsync(text).ConfigureAwait(true);
        return true;
    }

    private TopLevel? ResolveTopLevel(object? sender)
    {
        if (sender is Control source && TopLevel.GetTopLevel(source) is { } senderTopLevel)
        {
            return senderTopLevel;
        }

        if (TopLevel.GetTopLevel(this) is { } viewTopLevel)
        {
            return viewTopLevel;
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
