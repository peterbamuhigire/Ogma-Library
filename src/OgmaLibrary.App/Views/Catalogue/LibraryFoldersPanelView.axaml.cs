using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the Library folders panel (Sept-23 Phase 05).</summary>
public partial class LibraryFoldersPanelView : UserControl
{
    /// <summary>Initializes the panel.</summary>
    public LibraryFoldersPanelView()
    {
        InitializeComponent();
    }

    private LibraryFoldersViewModel? ViewModel => DataContext as LibraryFoldersViewModel;

    private void AddFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(AddFolderAsync, "library.add_folder_click");

    private async Task AddFolderAsync()
    {
        if (ViewModel is not { } vm || TopLevel.GetTopLevel(this) is not { } topLevel ||
            !topLevel.StorageProvider.CanOpen)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = vm.AddFolderText,
                AllowMultiple = false,
            }).ConfigureAwait(true);
        if (folders.Count > 0)
        {
            await vm.AddFolderAsync(folders[0].Path.LocalPath).ConfigureAwait(true);
        }
    }

    private void RescanAll_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm)
                {
                    await vm.RescanAsync().ConfigureAwait(true);
                }
            },
            "library.rescan_all_click");

    private void RescanFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && FolderOf(sender) is { } folder)
                {
                    await vm.RescanAsync(folder).ConfigureAwait(true);
                }
            },
            "library.rescan_folder_click");

    private void RemoveFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && FolderOf(sender) is { } folder)
                {
                    await vm.RemoveFolderAsync(folder).ConfigureAwait(true);
                }
            },
            "library.remove_folder_click");

    private void IncludeFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && sender is CheckBox box && FolderOf(sender) is { } folder)
                {
                    await vm.SetIncludedAsync(folder, box.IsChecked == true).ConfigureAwait(true);
                }
            },
            "library.include_folder_click");

    private void RetryIssue_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && EntryOf(sender) is { } entry)
                {
                    await vm.RetryAsync(entry).ConfigureAwait(true);
                }
            },
            "library.retry_issue_click");

    private void ShowIssue_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && EntryOf(sender) is { } entry)
                {
                    await vm.ShowInFolderAsync(entry).ConfigureAwait(true);
                }
            },
            "library.show_issue_click");

    private void IgnoreIssue_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            async () =>
            {
                if (ViewModel is { } vm && EntryOf(sender) is { } entry)
                {
                    await vm.IgnoreAsync(entry).ConfigureAwait(true);
                }
            },
            "library.ignore_issue_click");

    private static LibraryFolderItem? FolderOf(object? sender) =>
        (sender as Control)?.DataContext as LibraryFolderItem;

    private static NeedsAttentionEntry? EntryOf(object? sender) =>
        (sender as Control)?.DataContext as NeedsAttentionEntry;
}
