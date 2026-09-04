using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.App.ViewModels.Reader;

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

    private async void PageScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
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

    private async void FirstButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
        {
            await vm.GoFirstAsync().ConfigureAwait(true);
        }
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

    private async void LastButton_Click(object? sender, RoutedEventArgs e)
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

    private async void PageNumberBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ReaderViewModel vm)
        {
            return;
        }

        await vm.JumpToPageAsync().ConfigureAwait(true);
        e.Handled = true;
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

    private async void ExportReaderStateButton_Click(object? sender, RoutedEventArgs e)
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

    private async void ImportReaderStateButton_Click(object? sender, RoutedEventArgs e)
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

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
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
