using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>Code-behind for the reader surface and Phase 09 side panels.</summary>
public partial class ReaderView : UserControl
{
    private bool _isSelectingPageText;

    /// <summary>Initializes a new instance of <see cref="ReaderView"/>.</summary>
    public ReaderView()
    {
        InitializeComponent();
    }

    private async void PreviousButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoPreviousAsync().ConfigureAwait(true);
        }
    }

    private async void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoNextAsync().ConfigureAwait(true);
        }
    }

    private async void AddBookmarkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AddBookmarkAsync().ConfigureAwait(true);
        }
    }

    private async void CaptureCitationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CaptureCitationAsync().ConfigureAwait(true);
        }
    }

    private async void SelectionHighlightButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateHighlightFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private async void SelectionNoteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CreateNoteFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private async void SelectionCitationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.CaptureCitationFromSelectionAsync().ConfigureAwait(true);
        }
    }

    private async void CopyCitationButton_Click(object? sender, RoutedEventArgs e)
    {
        await CopyCitationToClipboardAsync().ConfigureAwait(true);
    }

    private async void ExportCitationButton_Click(object? sender, RoutedEventArgs e)
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

    private async void CreateHighlightButton_Click(object? sender, RoutedEventArgs e)
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

    private async void CreateNoteButton_Click(object? sender, RoutedEventArgs e)
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

    private async void ConfirmDeleteAnnotationButton_Click(object? sender, RoutedEventArgs e)
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

    private async void NoteEditor_LostFocus(object? sender, RoutedEventArgs e)
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

    private async void AddLayerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AddLayerAsync().ConfigureAwait(true);
        }
    }

    private async void LayerVisibility_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm ||
            sender is not CheckBox { DataContext: LayerListItem layer } checkBox)
        {
            return;
        }

        await vm.SetLayerVisibilityAsync(layer, checkBox.IsChecked == true).ConfigureAwait(true);
    }

    private async void LayerFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is ComboBox { SelectedItem: LayerFilterOption option })
        {
            await vm.SelectLayerFilterAsync(option).ConfigureAwait(true);
        }
    }

    private async void LayerName_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is TextBox { DataContext: LayerListItem layer } textBox)
        {
            await vm.RenameLayerAsync(layer, textBox.Text).ConfigureAwait(true);
        }
    }

    private async void DeleteLayerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: LayerListItem layer })
        {
            await vm.DeleteLayerAsync(layer).ConfigureAwait(true);
        }
    }

    private async void MergeLayerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: LayerListItem layer })
        {
            await vm.MergeLayerIntoFirstAvailableAsync(layer).ConfigureAwait(true);
        }
    }

    private async void SaveMemoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private async void ReadingMemoryField_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.AutoSaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private async void BookmarkLabel_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is TextBox { DataContext: BookmarkListItem bookmark } textBox)
        {
            await vm.RenameBookmarkAsync(bookmark, textBox.Text).ConfigureAwait(true);
        }
    }

    private async void DeleteBookmarkButton_Click(object? sender, RoutedEventArgs e)
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

    private async void DeleteBookmarkMenuItem_Click(object? sender, RoutedEventArgs e)
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

    private async void BookmarkNavigateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm &&
            sender is Button { DataContext: BookmarkListItem bookmark })
        {
            await vm.NavigateToBookmarkAsync(bookmark).ConfigureAwait(true);
        }
    }

    private async void Bookmarks_KeyDown(object? sender, KeyEventArgs e)
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

    private async void ReaderView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm ||
            !e.KeyModifiers.HasFlag(KeyModifiers.Control))
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
