using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.Infrastructure;
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

    private void ConfirmStartButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmStartButton_ClickAsync(sender, e), "sharing.confirm_start_button_click");

    private async Task ConfirmStartButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmStartAsync().ConfigureAwait(true);
        }
    }

    private void CancelStartButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.CancelStartConfirmation();

    private void StopButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => StopButton_ClickAsync(sender, e), "sharing.stop_button_click");

    private async Task StopButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.StopAsync().ConfigureAwait(true);
        }
    }

    private void ConnectToHostButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConnectToHostButton_ClickAsync(sender, e), "sharing.connect_to_host_button_click");

    private async Task ConnectToHostButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConnectToHostAsync().ConfigureAwait(true);
        }
    }

    private void DiscoverHostsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DiscoverHostsButton_ClickAsync(sender, e), "sharing.discover_hosts_button_click");

    private async Task DiscoverHostsButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DiscoverHostsAsync().ConfigureAwait(true);
        }
    }

    private void SyncNowButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SyncNowButton_ClickAsync(sender, e), "sharing.sync_now_button_click");

    private async Task SyncNowButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SyncNowAsync().ConfigureAwait(true);
        }
    }

    private void RefreshSchoolAdminButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RefreshSchoolAdminButton_ClickAsync(sender, e), "sharing.refresh_school_admin_button_click");

    private async Task RefreshSchoolAdminButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RefreshSchoolAdminAsync().ConfigureAwait(true);
        }
    }

    private void SaveSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveSchoolAiKeyButton_ClickAsync(sender, e), "sharing.save_school_ai_key_button_click");

    private async Task SaveSchoolAiKeyButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        char[] key = (SchoolAiKeyBox.Text ?? string.Empty).ToCharArray();
        SchoolAiKeyBox.Text = string.Empty;
        await ViewModel.SaveSchoolAiKeyAsync(key).ConfigureAwait(true);
    }

    private void DeleteSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteSchoolAiKeyButton_ClickAsync(sender, e), "sharing.delete_school_ai_key_button_click");

    private async Task DeleteSchoolAiKeyButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteSchoolAiKeyAsync().ConfigureAwait(true);
        }
    }

    private void TestSchoolAiKeyButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => TestSchoolAiKeyButton_ClickAsync(sender, e), "sharing.test_school_ai_key_button_click");

    private async Task TestSchoolAiKeyButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.TestSchoolAiKeyAsync().ConfigureAwait(true);
        }
    }

    private void SaveSchoolAiPolicyButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveSchoolAiPolicyButton_ClickAsync(sender, e), "sharing.save_school_ai_policy_button_click");

    private async Task SaveSchoolAiPolicyButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SaveSchoolAiPolicyAsync().ConfigureAwait(true);
        }
    }

    private void EnrollProfileButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => EnrollProfileButton_ClickAsync(sender, e), "sharing.enroll_profile_button_click");

    private async Task EnrollProfileButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.EnrollProfileAsync().ConfigureAwait(true);
        }
    }

    private void RevokeProfileButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RevokeProfileButton_ClickAsync(sender, e), "sharing.revoke_profile_button_click");

    private async Task RevokeProfileButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RevokeSelectedProfileAsync().ConfigureAwait(true);
        }
    }

    private void PurgeAiHistoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PurgeAiHistoryButton_ClickAsync(sender, e), "sharing.purge_ai_history_button_click");

    private async Task PurgeAiHistoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.PurgeAiHistoryAsync().ConfigureAwait(true);
        }
    }

    private void ExportSchoolAuditCsvButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportSchoolAuditCsvButton_ClickAsync(sender, e), "sharing.export_school_audit_csv_button_click");

    private async Task ExportSchoolAuditCsvButton_ClickAsync(object? sender, RoutedEventArgs e)
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

    private void ConfirmOfflineCacheClearButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmOfflineCacheClearButton_ClickAsync(sender, e), "sharing.confirm_offline_cache_clear_button_click");

    private async Task ConfirmOfflineCacheClearButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmOfflineCacheClearAsync().ConfigureAwait(true);
        }
    }

    private void ExportOfflineCacheButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportOfflineCacheButton_ClickAsync(sender, e), "sharing.export_offline_cache_button_click");

    private async Task ExportOfflineCacheButton_ClickAsync(object? sender, RoutedEventArgs e)
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

    private void SyncSettingsCheckBox_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SyncSettingsCheckBox_ClickAsync(sender, e), "sharing.sync_settings_check_box_click");

    private async Task SyncSettingsCheckBox_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SaveSyncSettingsAsync().ConfigureAwait(true);
        }
    }

    private void KeepLocalConflictButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => KeepLocalConflictButton_ClickAsync(sender, e), "sharing.keep_local_conflict_button_click");

    private async Task KeepLocalConflictButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.KeepLocalAnnotationConflictAsync().ConfigureAwait(true);
        }
    }

    private void KeepServerConflictButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => KeepServerConflictButton_ClickAsync(sender, e), "sharing.keep_server_conflict_button_click");

    private async Task KeepServerConflictButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.KeepServerAnnotationConflictAsync().ConfigureAwait(true);
        }
    }

    private void CopyJoinLinkButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CopyJoinLinkButton_ClickAsync(sender, e), "sharing.copy_join_link_button_click");

    private async Task CopyJoinLinkButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null &&
            await CopyTextAsync(sender, ViewModel.ManualJoinUri).ConfigureAwait(true))
        {
            ViewModel.MarkJoinLinkCopied();
        }
    }

    private void CopyFingerprintButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CopyFingerprintButton_ClickAsync(sender, e), "sharing.copy_fingerprint_button_click");

    private async Task CopyFingerprintButton_ClickAsync(object? sender, RoutedEventArgs e)
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
