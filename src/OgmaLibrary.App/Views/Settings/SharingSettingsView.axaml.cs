using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void RefreshSchoolAdminButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RefreshSchoolAdminAsync().ConfigureAwait(true);
        }
    }

    private async void SaveSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        char[] key = (SchoolAiKeyBox.Text ?? string.Empty).ToCharArray();
        SchoolAiKeyBox.Text = string.Empty;
        await ViewModel.SaveSchoolAiKeyAsync(key).ConfigureAwait(true);
    }

    private async void DeleteSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteSchoolAiKeyAsync().ConfigureAwait(true);
        }
    }

    private async void TestSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.TestSchoolAiKeyAsync().ConfigureAwait(true);
        }
    }

    private async void SaveSchoolAiPolicyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SaveSchoolAiPolicyAsync().ConfigureAwait(true);
        }
    }

    private async void EnrollProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.EnrollProfileAsync().ConfigureAwait(true);
        }
    }

    private async void RevokeProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RevokeSelectedProfileAsync().ConfigureAwait(true);
        }
    }

    private async void PurgeAiHistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.PurgeAiHistoryAsync().ConfigureAwait(true);
        }
    }

    private async void ExportSchoolAuditCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            using var stream = new MemoryStream();
            await ViewModel.ExportSchoolAuditCsvAsync(stream).ConfigureAwait(true);
        }
    }

    private void RequestOfflineCacheClearButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.RequestOfflineCacheClearConfirmation();

    private void CancelOfflineCacheClearButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.CancelOfflineCacheClearConfirmation();

    private async void ConfirmOfflineCacheClearButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmOfflineCacheClearAsync().ConfigureAwait(true);
        }
    }

    private async void ExportOfflineCacheButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        TopLevel? topLevel = ResolveTopLevel(sender);
        if (topLevel?.StorageProvider.CanSave != true)
        {
            ViewModel.ReportOfflineCacheStorageUnavailable();
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = "ogma-classroom-cache.zip",
                DefaultExtension = "zip",
                FileTypeChoices =
                [
                    new FilePickerFileType("ZIP")
                    {
                        Patterns = ["*.zip"],
                        MimeTypes = ["application/zip"],
                    },
                ],
            }).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        Stream stream = await file.OpenWriteAsync().ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await ViewModel.ExportOfflineCacheAsync(stream).ConfigureAwait(true);
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
