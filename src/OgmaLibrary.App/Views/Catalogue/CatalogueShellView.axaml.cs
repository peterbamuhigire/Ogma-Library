using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the main catalogue shell (FR-CAT-001).</summary>
public partial class CatalogueShellView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="CatalogueShellView"/>.</summary>
    public CatalogueShellView()
    {
        InitializeComponent();
    }

    private void SidebarToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.ToggleSidebar();
        }
    }

    private void FilterToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.ToggleFilterPanel();
        }
    }

    private void SearchToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.ToggleSearchPanel();
            if (vm.IsSearchPanelOpen)
            {
                FocusSearchPanel();
            }
        }
    }

    private void AddLooseFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (DataContext is MainShellViewModel vm)
                {
                    await vm.AddLooseFolderAsync().ConfigureAwait(true);
                }
            },
            "catalogue.add_loose_folder_click");

    private void IndexManagerToggle_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => IndexManagerToggle_ClickAsync(sender, e), "catalogue.index_manager_toggle_click");

    private async Task IndexManagerToggle_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ToggleIndexManagerAsync().ConfigureAwait(true);
        }
    }

    private void CatalogueShellView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainShellViewModel vm)
        {
            return;
        }

        if (e.Key == Key.F5)
        {
            // Sept-23 Phase 05 (T05.8): keyboard access to Rescan library.
            UiActions.Run(vm.RescanLibraryAsync, "catalogue.rescan_key");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (vm.IsSearchPanelOpen)
            {
                vm.IsSearchPanelOpen = false;
                e.Handled = true;
                return;
            }

            if (vm.IsIndexManagerOpen)
            {
                vm.IsIndexManagerOpen = false;
                e.Handled = true;
            }

            return;
        }

        if (!(e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) ||
            e.Key is not (Key.F or Key.K))
        {
            return;
        }

        if (!vm.IsSearchPanelOpen)
        {
            vm.ToggleSearchPanel();
        }

        FocusSearchPanel();
        e.Handled = true;
    }

    private void FocusSearchPanel() =>
        Dispatcher.UIThread.Post(() => SearchPanel.FocusSearchBox());

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.Filter.ClearAll();
        }
    }

    private void ReconciliationReviewToggle_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ReconciliationReviewToggle_ClickAsync(sender, e), "catalogue.reconciliation_review_toggle_click");

    private async Task ReconciliationReviewToggle_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ToggleReconciliationReviewsAsync().ConfigureAwait(true);
        }
    }

    private void ReconciliationReviewPanel_CloseRequested(object? sender, EventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.CloseReconciliationReviews();
        }
    }

    private void ToggleSortDirection_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.Filter.ToggleSortDirection();
        }
    }

    private void PreviousPage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.GoToPreviousPage();
        }
    }

    private void NextPage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.GoToNextPage();
        }
    }

    private void GridViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.Grid;
        }
    }

    private void LibraryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LibraryButton_ClickAsync(sender, e), "catalogue.library_button_click");

    private async Task LibraryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ReturnToLibraryAsync().ConfigureAwait(true);
        }
    }

    private void ListViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.List;
        }
    }

    private void DirectoryViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.Directory;
        }
    }

    private void SplitViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.OpenSplitViewScaffold();
        }
    }

    private void StudentSmartSearchButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.OpenStudentSmartSearch();
        }
    }

    private void AdvisorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.OpenAdvisor();
        }
    }

    private void ReadingPlanButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.OpenReadingPlan();
        }
    }

    private void Bookshelf3DButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.OpenBookshelf3D();
        }
    }

    private void SharingSettingsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SharingSettingsButton_ClickAsync(sender, e), "catalogue.sharing_settings_button_click");

    private async Task SharingSettingsButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.OpenSharingSettingsAsync().ConfigureAwait(true);
        }
    }

    private void ChooseFolderButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ChooseFolderButton_ClickAsync(sender, e), "catalogue.choose_folder_button_click");

    private async Task ChooseFolderButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            var topLevel = ResolveTopLevel(sender);

            if (topLevel is not null)
            {
                await vm.ChooseFolderAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportChooseFolderUnavailable();
            }
        }
    }

    private void OpenPdfButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenPdfButton_ClickAsync(sender, e), "catalogue.open_pdf_button_click");

    private async Task OpenPdfButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            var topLevel = ResolveTopLevel(sender);

            if (topLevel is not null)
            {
                await vm.OpenPdfAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportOpenPdfUnavailable();
            }
        }
    }

    private void HostStartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            vm.HostSharing.RequestStartConfirmation();
        }
    }

    private void HostConfirmStartButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => HostConfirmStartButton_ClickAsync(sender, e), "catalogue.host_confirm_start_button_click");

    private async Task HostConfirmStartButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            await vm.HostSharing.ConfirmStartAsync().ConfigureAwait(true);
        }
    }

    private void HostCancelStartConfirmationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            vm.HostSharing.CancelStartConfirmation();
        }
    }

    private void HostStopButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => HostStopButton_ClickAsync(sender, e), "catalogue.host_stop_button_click");

    private async Task HostStopButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            await vm.HostSharing.StopAsync().ConfigureAwait(true);
        }
    }

    private void HostShareButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            vm.HostSharing.OpenSharePanel();
        }
    }

    private void HostCloseSharePanelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            vm.HostSharing.CloseSharePanel();
        }
    }

    private void HostCopyJoinLinkButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => HostCopyJoinLinkButton_ClickAsync(sender, e), "catalogue.host_copy_join_link_button_click");

    private async Task HostCopyJoinLinkButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            if (await CopyHostShareTextAsync(sender, vm.HostSharing.ManualJoinUri).ConfigureAwait(true))
            {
                vm.HostSharing.MarkJoinLinkCopied();
            }
        }
    }

    private void HostCopyFingerprintButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => HostCopyFingerprintButton_ClickAsync(sender, e), "catalogue.host_copy_fingerprint_button_click");

    private async Task HostCopyFingerprintButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            if (await CopyHostShareTextAsync(sender, vm.HostSharing.FullFingerprintText).ConfigureAwait(true))
            {
                vm.HostSharing.MarkFingerprintCopied();
            }
        }
    }

    private async Task<bool> CopyHostShareTextAsync(object? sender, string text)
    {
        if (DataContext is not MainShellViewModel { HostSharing: not null } vm)
        {
            return false;
        }

        var topLevel = ResolveTopLevel(sender);
        if (topLevel?.Clipboard is null)
        {
            vm.HostSharing.ReportClipboardUnavailable();
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

    private void CreateShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CreateShelf_ClickAsync(sender, e), "catalogue.create_shelf_click");

    private async Task CreateShelf_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ShelfSidebar.CreateNewShelfAsync().ConfigureAwait(true);
        }
    }

    private void DeleteShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteShelf_ClickAsync(sender, e), "catalogue.delete_shelf_click");

    private async Task DeleteShelf_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ShelfSidebar.DeleteSelectedShelfAsync().ConfigureAwait(true);
        }
    }

    private void RenameShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RenameShelf_ClickAsync(sender, e), "catalogue.rename_shelf_click");

    private async Task RenameShelf_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.ShelfSidebar.RenameSelectedShelfAsync().ConfigureAwait(true);
        }
    }
}
