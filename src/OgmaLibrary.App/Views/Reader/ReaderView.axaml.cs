using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>Code-behind for the reader surface and Phase 09 side panels.</summary>
public partial class ReaderView : UserControl
{
    private bool _isSelectingPageText;
    private ReaderViewModel? _boundViewModel;

    /// <summary>Initializes a new instance of <see cref="ReaderView"/>.</summary>
    public ReaderView()
    {
        InitializeComponent();
        DataContextChanged += ReaderView_DataContextChanged;
        PageScrollViewer.SizeChanged += PageScrollViewer_SizeChanged;
        PageScrollViewer.ScrollChanged += PageScrollViewer_ScrollChanged;

        // Let the ScrollViewer consume ordinary wheel input for smooth,
        // continuous movement. Only a gesture at an actual page boundary turns
        // the document, and it still receives events that the presenter handled.
        PageScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            PageScrollViewer_PointerWheelChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void ReaderView_DataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= ReaderViewModel_PropertyChanged;
        }

        _boundViewModel = DataContext as ReaderViewModel;
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged += ReaderViewModel_PropertyChanged;
        }
    }

    private void ReaderViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReaderViewModel.CurrentPageIndex))
        {
            PageScrollViewer.Offset = new Vector(0, 0);
        }
    }

    private void PageScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.UpdatePageViewport(e.NewSize.Width, e.NewSize.Height);
        }
    }

    private void PageScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            double maximumOffset = Math.Max(0.0, PageScrollViewer.Extent.Height - PageScrollViewer.Viewport.Height);
            double normalizedOffset = maximumOffset <= 0.0
                ? 0.0
                : Math.Clamp(PageScrollViewer.Offset.Y / maximumOffset, 0.0, 1.0);
            vm.UpdateScrollOffset(normalizedOffset);
        }
    }

    private void PageScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e) =>
        UiActions.Run(() => PageScrollViewer_PointerWheelChangedAsync(sender, e), "reader.page_scroll_viewer_pointer_wheel_changed");

    private async Task PageScrollViewer_PointerWheelChangedAsync(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm)
        {
            return;
        }

        double maximumOffset = Math.Max(0.0, PageScrollViewer.Extent.Height - PageScrollViewer.Viewport.Height);
        bool atTop = PageScrollViewer.Offset.Y <= 1.0;
        bool atBottom = PageScrollViewer.Offset.Y >= maximumOffset - 1.0;

        if (e.Delta.Y < 0 && atBottom && vm.CanGoNext)
        {
            e.Handled = true;
            await vm.GoNextAsync().ConfigureAwait(true);
        }
        else if (e.Delta.Y > 0 && atTop && vm.CanGoPrevious)
        {
            e.Handled = true;
            await vm.GoPreviousAsync().ConfigureAwait(true);
        }
    }

    private void FirstButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => FirstButton_ClickAsync(sender, e), "reader.first_button_click");

    private async Task FirstButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoFirstAsync().ConfigureAwait(true);
        }
    }

    private void PreviousButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PreviousButton_ClickAsync(sender, e), "reader.previous_button_click");

    private async Task PreviousButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoPreviousAsync().ConfigureAwait(true);
        }
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => NextButton_ClickAsync(sender, e), "reader.next_button_click");

    private async Task NextButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoNextAsync().ConfigureAwait(true);
        }
    }

    private void RetryRenderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.RetryRender();
        }
    }

    private void LastButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LastButton_ClickAsync(sender, e), "reader.last_button_click");

    private async Task LastButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoLastAsync().ConfigureAwait(true);
        }
    }

    private void ZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.ZoomOut();
        }
    }

    private void ZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.ZoomIn();
        }
    }

    private void FitWidthButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.SetZoomMode(ZoomMode.FitWidth);
        }
    }

    private void FitPageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.SetZoomMode(ZoomMode.FitPage);
        }
    }

    private void PageNumberBox_KeyDown(object? sender, KeyEventArgs e) =>
        UiActions.Run(() => PageNumberBox_KeyDownAsync(sender, e), "reader.page_number_box_key_down");

    private async Task PageNumberBox_KeyDownAsync(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ReaderViewModel vm)
        {
            return;
        }

        await vm.JumpToPageAsync().ConfigureAwait(true);
        e.Handled = true;
    }

    private void AddBookmarkButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => AddBookmarkButton_ClickAsync(sender, e), "reader.add_bookmark_button_click");

    private async Task AddBookmarkButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AddBookmarkAsync().ConfigureAwait(true);
        }
    }

    private void CaptureCitationButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CaptureCitationButton_ClickAsync(sender, e), "reader.capture_citation_button_click");

    private async Task CaptureCitationButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CaptureCitationAsync().ConfigureAwait(true);
        }
    }

    private void ExportReaderStateButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportReaderStateButton_ClickAsync(sender, e), "reader.export_reader_state_button_click");

    private async Task ExportReaderStateButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm || !vm.CanUsePortability)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider.CanSave != true)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = $"ogma-{vm.BookId}-reader-state.json",
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"],
                        MimeTypes = ["application/json"],
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
            await vm.ExportReaderStateAsync(stream).ConfigureAwait(true);
        }
    }

    private void ImportReaderStateButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ImportReaderStateButton_ClickAsync(sender, e), "reader.import_reader_state_button_click");

    private async Task ImportReaderStateButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm || !vm.CanUsePortability)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider.CanOpen != true)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"],
                        MimeTypes = ["application/json"],
                    },
                ],
            }).ConfigureAwait(true);
        IStorageFile? file = files.Count == 0 ? null : files[0];
        if (file is null)
        {
            return;
        }

        Stream stream = await file.OpenReadAsync().ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await vm.ImportReaderStateAsync(stream).ConfigureAwait(true);
        }
    }

    private void SelectionHighlightButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SelectionHighlightButton_ClickAsync(sender, e), "reader.selection_highlight_button_click");

    private async Task SelectionHighlightButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateHighlightFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private void SelectionNoteButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SelectionNoteButton_ClickAsync(sender, e), "reader.selection_note_button_click");

    private async Task SelectionNoteButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateNoteFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private void SelectionCitationButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SelectionCitationButton_ClickAsync(sender, e), "reader.selection_citation_button_click");

    private async Task SelectionCitationButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CaptureCitationFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private void CopyCitationButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CopyCitationButton_ClickAsync(sender, e), "reader.copy_citation_button_click");

    private async Task CopyCitationButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        await CopyCitationToClipboardAsync().ConfigureAwait(true);
    }

    private void ExportCitationButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportCitationButton_ClickAsync(sender, e), "reader.export_citation_button_click");

    private async Task ExportCitationButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        await CopyCitationToClipboardAsync().ConfigureAwait(true);

        if (DataContext is ReaderViewModel vm)
        {
            await vm.ExportCitationAsync().ConfigureAwait(true);
        }
    }

    private void CloseCitationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.CloseCitationCard();
        }
    }

    private void CreateHighlightButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CreateHighlightButton_ClickAsync(sender, e), "reader.create_highlight_button_click");

    private async Task CreateHighlightButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateHighlightAsync().ConfigureAwait(true);
        }
    }

    private void HighlightColorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: HighlightColorOption option })
        {
            vm.SelectHighlightColor(option);
        }
    }

    private void CreateNoteButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CreateNoteButton_ClickAsync(sender, e), "reader.create_note_button_click");

    private async Task CreateNoteButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateNoteAsync().ConfigureAwait(true);
        }
    }

    private void DeleteAnnotationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: AnnotationListItem annotation })
        {
            vm.RequestDeleteAnnotation(annotation);
        }
    }

    private void DeleteAnnotationMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is MenuItem { CommandParameter: AnnotationListItem annotation })
        {
            vm.RequestDeleteAnnotation(annotation);
        }
    }

    private void ConfirmDeleteAnnotationButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmDeleteAnnotationButton_ClickAsync(sender, e), "reader.confirm_delete_annotation_button_click");

    private async Task ConfirmDeleteAnnotationButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.ConfirmDeleteAnnotationAsync().ConfigureAwait(true);
        }
    }

    private void CancelDeleteAnnotationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            vm.CancelDeleteAnnotation();
        }
    }

    private void EditNoteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: AnnotationListItem annotation })
        {
            vm.OpenNoteEditor(annotation);
        }
    }

    private void NoteAnchorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: AnnotationOverlayItem overlay })
        {
            vm.OpenNoteEditorById(overlay.AnnotationId);
            e.Handled = true;
        }
    }

    private void NoteEditor_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => NoteEditor_LostFocusAsync(sender, e), "reader.note_editor_lost_focus");

    private async Task NoteEditor_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.SaveOpenNoteAsync().ConfigureAwait(true);
        }
    }

    private void NoteEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not ReaderViewModel vm)
        {
            return;
        }

        vm.CloseNoteEditor();
        e.Handled = true;
    }

    private void AddLayerButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => AddLayerButton_ClickAsync(sender, e), "reader.add_layer_button_click");

    private async Task AddLayerButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AddLayerAsync().ConfigureAwait(true);
        }
    }

    private void LayerVisibility_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LayerVisibility_ClickAsync(sender, e), "reader.layer_visibility_click");

    private async Task LayerVisibility_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm ||
            sender is not CheckBox { DataContext: LayerListItem layer } checkBox)
        {
            return;
        }

        await vm.SetLayerVisibilityAsync(layer, checkBox.IsChecked == true).ConfigureAwait(true);
    }

    private void LayerFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UiActions.Run(() => LayerFilter_SelectionChangedAsync(sender, e), "reader.layer_filter_selection_changed");

    private async Task LayerFilter_SelectionChangedAsync(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is ComboBox { SelectedItem: LayerFilterOption option })
        {
            await vm.SelectLayerFilterAsync(option).ConfigureAwait(true);
        }
    }

    private void LayerName_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LayerName_LostFocusAsync(sender, e), "reader.layer_name_lost_focus");

    private async Task LayerName_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is TextBox { DataContext: LayerListItem layer } textBox)
        {
            await vm.RenameLayerAsync(layer, textBox.Text).ConfigureAwait(true);
        }
    }

    private void DeleteLayerButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteLayerButton_ClickAsync(sender, e), "reader.delete_layer_button_click");

    private async Task DeleteLayerButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: LayerListItem layer })
        {
            await vm.DeleteLayerAsync(layer).ConfigureAwait(true);
        }
    }

    private void MergeLayerButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => MergeLayerButton_ClickAsync(sender, e), "reader.merge_layer_button_click");

    private async Task MergeLayerButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: LayerListItem layer })
        {
            await vm.MergeLayerIntoFirstAvailableAsync(layer).ConfigureAwait(true);
        }
    }

    private void SaveMemoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveMemoryButton_ClickAsync(sender, e), "reader.save_memory_button_click");

    private async Task SaveMemoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void ReadingMemoryField_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ReadingMemoryField_LostFocusAsync(sender, e), "reader.reading_memory_field_lost_focus");

    private async Task ReadingMemoryField_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AutoSaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void BookmarkLabel_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => BookmarkLabel_LostFocusAsync(sender, e), "reader.bookmark_label_lost_focus");

    private async Task BookmarkLabel_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is TextBox { DataContext: BookmarkListItem bookmark } textBox)
        {
            await vm.RenameBookmarkAsync(bookmark, textBox.Text).ConfigureAwait(true);
        }
    }

    private void DeleteBookmarkButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteBookmarkButton_ClickAsync(sender, e), "reader.delete_bookmark_button_click");

    private async Task DeleteBookmarkButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: BookmarkListItem bookmark })
        {
            await vm.DeleteBookmarkAsync(bookmark).ConfigureAwait(true);
        }
    }

    private void RenameBookmarkMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            BookmarkFromMenuItem(menuItem) is not { } bookmark)
        {
            return;
        }

        TextBox? editor = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(textBox => ReferenceEquals(textBox.DataContext, bookmark));

        if (editor is null)
        {
            return;
        }

        editor.Focus();
        editor.SelectAll();
        e.Handled = true;
    }

    private void DeleteBookmarkMenuItem_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteBookmarkMenuItem_ClickAsync(sender, e), "reader.delete_bookmark_menu_item_click");

    private async Task DeleteBookmarkMenuItem_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is MenuItem menuItem &&
            BookmarkFromMenuItem(menuItem) is { } bookmark)
        {
            await vm.DeleteBookmarkAsync(bookmark).ConfigureAwait(true);
            e.Handled = true;
        }
    }

    private static BookmarkListItem? BookmarkFromMenuItem(MenuItem menuItem)
    {
        if (menuItem.CommandParameter is BookmarkListItem bookmark)
        {
            return bookmark;
        }

        return menuItem.Tag as BookmarkListItem
            ?? menuItem.DataContext as BookmarkListItem;
    }

    private void BookmarkNavigateButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => BookmarkNavigateButton_ClickAsync(sender, e), "reader.bookmark_navigate_button_click");

    private async Task BookmarkNavigateButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: BookmarkListItem bookmark })
        {
            await vm.NavigateToBookmarkAsync(bookmark).ConfigureAwait(true);
        }
    }

    private void Bookmarks_KeyDown(object? sender, KeyEventArgs e) =>
        UiActions.Run(() => Bookmarks_KeyDownAsync(sender, e), "reader.bookmarks_key_down");

    private async Task Bookmarks_KeyDownAsync(object? sender, KeyEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            MoveBookmarkSelection(listBox, e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter ||
            DataContext is not ReaderViewModel vm ||
            listBox.SelectedItem is not BookmarkListItem bookmark)
        {
            return;
        }

        await vm.NavigateToBookmarkAsync(bookmark).ConfigureAwait(true);
        e.Handled = true;
    }

    private static void MoveBookmarkSelection(ListBox listBox, int direction)
    {
        int count = listBox.ItemCount;
        if (count == 0)
        {
            return;
        }

        int current = listBox.SelectedIndex < 0 ? (direction > 0 ? -1 : 0) : listBox.SelectedIndex;
        listBox.SelectedIndex = Math.Clamp(current + direction, 0, count - 1);
    }

    private void PageSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm ||
            CanTrackSelectionPointer(
                e.Pointer.Type,
                e.GetCurrentPoint(PageSurface).Properties.IsLeftButtonPressed) is false)
        {
            return;
        }

        var position = e.GetPosition(PageSurface);
        _isSelectingPageText = true;
        vm.BeginSelection(position.X, position.Y);
        e.Handled = true;
    }

    private void PageSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSelectingPageText ||
            DataContext is not ReaderViewModel vm ||
            CanTrackSelectionPointer(
                e.Pointer.Type,
                e.GetCurrentPoint(PageSurface).Properties.IsLeftButtonPressed) is false)
        {
            return;
        }

        var position = e.GetPosition(PageSurface);
        vm.UpdateSelection(position.X, position.Y);
        e.Handled = true;
    }

    private void PageSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSelectingPageText || DataContext is not ReaderViewModel vm)
        {
            return;
        }

        var position = e.GetPosition(PageSurface);
        vm.UpdateSelection(position.X, position.Y);
        vm.CompleteSelection();
        _isSelectingPageText = false;
        e.Handled = true;
    }

    /// <summary>
    /// Returns whether a pointer can drive text selection. Mouse selection must
    /// use the primary button; touch and pen drags do not expose that mouse flag.
    /// </summary>
    /// <param name="pointerType">The Avalonia pointer device type.</param>
    /// <param name="isLeftButtonPressed">Whether the mouse primary button is pressed.</param>
    /// <returns><see langword="true"/> when the pointer should update selection.</returns>
    public static bool CanTrackSelectionPointer(PointerType pointerType, bool isLeftButtonPressed) =>
        pointerType switch
        {
            PointerType.Mouse => isLeftButtonPressed,
            PointerType.Touch or PointerType.Pen => true,
            _ => false,
        };

    private void ReaderView_KeyDown(object? sender, KeyEventArgs e) =>
        UiActions.Run(() => ReaderView_KeyDownAsync(sender, e), "reader.reader_view_key_down");

    private async Task ReaderView_KeyDownAsync(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm)
        {
            return;
        }

        // Page navigation (no modifier). Text-input controls handle these keys
        // themselves, so focus inside an editor never reaches this bubbling handler.
        if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.PageDown:
                case Key.Down:
                case Key.Right:
                case Key.Space:
                    e.Handled = true;
                    await vm.GoNextAsync().ConfigureAwait(true);
                    return;
                case Key.PageUp:
                case Key.Up:
                case Key.Left:
                    e.Handled = true;
                    await vm.GoPreviousAsync().ConfigureAwait(true);
                    return;
            }
        }

        if (!(e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            return;
        }

        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            await vm.CaptureCitationAsync().ConfigureAwait(true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.B && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.OpenBookmarkPanel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.B)
        {
            await vm.ToggleBookmarkAsync().ConfigureAwait(true);
            e.Handled = true;
        }
    }

    private async Task CopyCitationToClipboardAsync()
    {
        if (DataContext is not ReaderViewModel vm ||
            string.IsNullOrWhiteSpace(vm.CitationPlainText))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(vm.CitationPlainText).ConfigureAwait(true);
        vm.MarkCitationCopied();
    }
}
