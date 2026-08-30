using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    private async void IndexManagerToggle_Click(object? sender, RoutedEventArgs e)
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

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key is not (Key.F or Key.K))
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

    private void GridViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.Grid;
        }
    }

    private async void LibraryButton_Click(object? sender, RoutedEventArgs e)
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

    private async void SharingSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            await vm.OpenSharingSettingsAsync().ConfigureAwait(true);
        }
    }

    private async void ChooseFolderButton_Click(object? sender, RoutedEventArgs e)
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

    private async void OpenPdfButton_Click(object? sender, RoutedEventArgs e)
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

    private async void HostConfirmStartButton_Click(object? sender, RoutedEventArgs e)
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

    private async void HostStopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            await vm.HostSharing.StopAsync().ConfigureAwait(true);
        }
    }

    private async void HostContentModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            await vm.HostSharing.RequestContentModeChangeAsync().ConfigureAwait(true);
        }
    }

    private async void HostConfirmFileStreamButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            await vm.HostSharing.ConfirmFileStreamAsync().ConfigureAwait(true);
        }
    }

    private void HostCancelFileStreamConfirmationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            vm.HostSharing.CancelFileStreamConfirmation();
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

    private async void HostCopyJoinLinkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel { HostSharing: not null } vm)
        {
            if (await CopyHostShareTextAsync(sender, vm.HostSharing.ManualJoinUri).ConfigureAwait(true))
            {
                vm.HostSharing.MarkJoinLinkCopied();
            }
        }
    }

    private async void HostCopyFingerprintButton_Click(object? sender, RoutedEventArgs e)
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
}
