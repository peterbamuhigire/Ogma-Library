using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the book-detail slide-in panel (FR-CAT-004).</summary>
public partial class BookDetailView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="BookDetailView"/>.</summary>
    public BookDetailView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.Close();
        }
    }

    private void PrimaryActionButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(PrimaryActionButton_ClickAsync, "catalogue.primary_action_click");

    private async Task PrimaryActionButton_ClickAsync()
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ExecutePrimaryActionAsync().ConfigureAwait(true);
        }
    }

    private void SectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm && sender is Button button &&
            Enum.TryParse(button.Tag?.ToString(), out InspectorSection section))
        {
            vm.SelectedSection = section;
        }
    }

    private void MoreMenu_Opening(object? sender, EventArgs e)
    {
        if (DataContext is not BookDetailViewModel { Book: { } book } ||
            CatalogueCardInteractions.FindShell(this)?.Selection is not { } selection)
        {
            AddToShelfMenuItem.IsEnabled = false;
            AddToShelfMenuItem.Items.Clear();
            return;
        }

        AddToShelfMenuItem.IsEnabled = true;
        CatalogueCardInteractions.PopulateAddToShelf(
            AddToShelfMenuItem, selection, [book.BookId], "Detail.Shelf", "catalogue.detail_add_to_shelf");
    }

    private void ShowInFolderButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(ShowInFolderButton_ClickAsync, "catalogue.show_in_folder_click");

    private async Task ShowInFolderButton_ClickAsync()
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ShowInFolderAsync().ConfigureAwait(true);
        }
    }

    private void ReadingStatusPicker_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UiActions.Run(ReadingStatusPicker_SelectionChangedAsync, "catalogue.reading_status_changed");

    private async Task ReadingStatusPicker_SelectionChangedAsync()
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SetReadingStatusIndexAsync(ReadingStatusPicker.SelectedIndex).ConfigureAwait(true);
        }
    }

    private void EnrichButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => EnrichButton_ClickAsync(sender, e), "catalogue.enrich_button_click");

    private async Task EnrichButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.SelectedSection = InspectorSection.Activity;
            await vm.EnrichMetadataAsync().ConfigureAwait(true);
        }
    }

    private void RunOcrButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RunOcrButton_ClickAsync(sender, e), "catalogue.run_ocr_button_click");

    private async Task RunOcrButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.SelectedSection = InspectorSection.Activity;
            await vm.RunOcrAsync().ConfigureAwait(true);
        }
    }

    private void ForgetPasswordButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ForgetPasswordButton_ClickAsync(sender, e), "catalogue.forget_password_button_click");

    private async Task ForgetPasswordButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ForgetPasswordAsync().ConfigureAwait(true);
        }
    }

    private void ReadingMemoryField_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ReadingMemoryField_LostFocusAsync(sender, e), "catalogue.reading_memory_field_lost_focus");

    private async Task ReadingMemoryField_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void SaveReadingMemoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveReadingMemoryButton_ClickAsync(sender, e), "catalogue.save_reading_memory_button_click");

    private async Task SaveReadingMemoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void LoadReadingHistoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LoadReadingHistoryButton_ClickAsync(sender, e), "catalogue.load_reading_history_button_click");

    private async Task LoadReadingHistoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadReadingHistoryAsync().ConfigureAwait(true);
        }
    }

    private void LoadTocButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LoadTocButton_ClickAsync(sender, e), "catalogue.load_toc_button_click");

    private async Task LoadTocButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadTocAsync().ConfigureAwait(true);
        }
    }

    private void PrepareWriteBackButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PrepareWriteBackButton_ClickAsync(sender, e), "catalogue.prepare_write_back_button_click");

    private async Task PrepareWriteBackButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm)
        {
            return;
        }

        // Outcomes that need no consent (no changes, missing file, failure) show under Activity.
        vm.SelectedSection = InspectorSection.Activity;
        await vm.PrepareWriteBackAsync().ConfigureAwait(true);
        if (!vm.HasPreparedWriteBack)
        {
            return;
        }

        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new WriteBackDialog { DataContext = dialog }.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            dialog.HandleClosed();
        }
    }

    private void RestoreWriteBackButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RestoreWriteBackButton_ClickAsync(sender, e), "catalogue.restore_write_back_button_click");

    private async Task RestoreWriteBackButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.RestoreWriteBackAsync().ConfigureAwait(true);
        }
    }

    private void LoadProvenanceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.LoadProvenance();
        }
    }

    // ── Inline field editing (Sept-23 Phase 11 slice 3) ────────────────────────

    private void FieldDisplayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DetailFieldViewModel field } button)
        {
            return;
        }

        field.BeginEdit();
        if (field.IsEditing && button.Parent is Grid grid && grid.Children.OfType<TextBox>().FirstOrDefault() is { } editor)
        {
            // The editor becomes visible on the next layout pass; focus it then.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                editor.Focus(Avalonia.Input.NavigationMethod.Tab);
                editor.SelectAll();
            });
        }
    }

    private void FieldEditor_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: DetailFieldViewModel field } editor)
        {
            return;
        }

        if (e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            UiActions.Run(() => CommitFieldAsync(editor, field, refocus: true), "catalogue.field_edit_commit");
        }
        else if (e.Key == Avalonia.Input.Key.Escape)
        {
            // Handled, so the shell's Esc does not also close the inspector.
            e.Handled = true;
            field.CancelEdit();
            FocusDisplayButton(editor);
        }
    }

    private void FieldEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: DetailFieldViewModel { State: DetailFieldState.Edit } field } editor)
        {
            UiActions.Run(() => CommitFieldAsync(editor, field, refocus: false), "catalogue.field_edit_blur");
        }
    }

    private static async Task CommitFieldAsync(TextBox editor, DetailFieldViewModel field, bool refocus)
    {
        await field.CommitAsync().ConfigureAwait(true);
        if (refocus && field.IsDisplaying)
        {
            FocusDisplayButton(editor);
        }
    }

    private static void FocusDisplayButton(TextBox editor)
    {
        if (editor.Parent is Grid grid && grid.Children.OfType<Button>().FirstOrDefault() is { } display)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => display.Focus(Avalonia.Input.NavigationMethod.Tab));
        }
    }

    private void FieldProvenanceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DetailFieldViewModel { IsProvenanceLoaded: false } field })
        {
            field.LoadProvenance();
        }
    }

    private void UndoFieldEditButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(UndoFieldEditButton_ClickAsync, "catalogue.field_edit_undo");

    private async Task UndoFieldEditButton_ClickAsync()
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.UndoFieldEditAsync().ConfigureAwait(true);
        }
    }

    private void OpenProviderAttributionButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenProviderAttributionButton_ClickAsync(sender, e), "catalogue.open_provider_attribution_button_click");

    private async Task OpenProviderAttributionButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProviderAttributionLink link } ||
            !Uri.TryCreate(link.Url, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host is not ("books.google.com" or "openlibrary.org"))
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            await topLevel.Launcher.LaunchUriAsync(uri).ConfigureAwait(true);
        }
    }

    private void RatingButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RatingButton_ClickAsync(sender, e), "catalogue.rating_button_click");

    private async Task RatingButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm || sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int rating))
        {
            return;
        }

        await vm.SetRatingAsync(rating).ConfigureAwait(true);
    }

    private void FavouriteButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => FavouriteButton_ClickAsync(sender, e), "catalogue.favourite_button_click");

    private async Task FavouriteButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ToggleFavouriteAsync().ConfigureAwait(true);
        }
    }

    private void SaveTagsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveTagsButton_ClickAsync(sender, e), "catalogue.save_tags_button_click");

    private async Task SaveTagsButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveTagsAsync().ConfigureAwait(true);
        }
    }

    private void AcceptMetadataProposalButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => AcceptMetadataProposalButton_ClickAsync(sender, e), "catalogue.accept_metadata_proposal_button_click");

    private async Task AcceptMetadataProposalButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.AcceptMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }

    private void RejectMetadataProposalButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RejectMetadataProposalButton_ClickAsync(sender, e), "catalogue.reject_metadata_proposal_button_click");

    private async Task RejectMetadataProposalButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.RejectMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }

    private void AcceptAllProposalsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => DataContext is BookDetailViewModel vm ? vm.AcceptAllProposalsAsync() : Task.CompletedTask,
            "catalogue.review.accept_all_click");

    private void UndoReviewButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => DataContext is BookDetailViewModel vm ? vm.UndoReviewAsync() : Task.CompletedTask,
            "catalogue.review.undo_click");
}
