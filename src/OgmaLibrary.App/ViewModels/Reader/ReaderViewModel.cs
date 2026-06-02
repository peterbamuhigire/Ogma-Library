using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Reader.Annotations;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>
/// View model for the reader surface and Phase 09 side panels. It opens the
/// durable reader session, then loads bookmarks, annotation layers, and reading
/// memory for the active book.
/// </summary>
public sealed class ReaderViewModel : INotifyPropertyChanged
{
    private readonly IReaderSessionService _sessions;
    private readonly IAnnotationService _annotations;
    private readonly IBookmarkService _bookmarks;
    private readonly IAnnotationLayerService _layers;
    private readonly ICitationService _citations;
    private readonly IReadingMemoryService _readingMemory;
    private readonly ILocalizationService _localization;
    private readonly ITextLayerService? _textLayers;
    private const int BookmarkTabIndex = 1;
    private const string BookmarkSortByPageId = "page";
    private const string BookmarkSortByCreatedId = "created";
    private const string AllVisibleLayersFilterId = "all-visible-layers";
    private const string LayerDefaultColorOptionId = "layer-default";
    private const double BasePageSurfaceWidth = 720.0;
    private const double BasePageSurfaceHeight = 960.0;
    private readonly Dictionary<string, AnnotationV2> _annotationsById = new(StringComparer.Ordinal);

    private string? _bookId;
    private int _currentPageIndex;
    private int _pageRotationDegrees;
    private int _pageCount;
    private ZoomMode _zoomMode = ZoomMode.FitWidth;
    private double _zoomPercent = 100.0;
    private int _selectedSidebarTabIndex;
    private bool _isTogglingBookmark;
    private CancellationTokenSource? _readingMemoryAutoSaveCts;
    private bool _isOpen;
    private bool _isBusy;
    private string? _statusMessage;
    private string? _openedBecause;
    private string? _keyInsight;
    private string? _openQuestions;
    private string? _dispositionText;
    private string? _selectedCitationText;
    private CitationCard? _currentCitationCard;
    private CitationCardItem? _citationCard;
    private AnnotationListItem? _selectedAnnotation;
    private AnnotationListItem? _editingNote;
    private AnnotationListItem? _pendingDeleteAnnotation;
    private BookmarkSortOption? _selectedBookmarkSortOption;
    private string? _editingNoteText;
    private string? _selectedHighlightColorOverride;
    private LayerFilterOption? _selectedLayerFilter;
    private SelectionOverlayItem? _selectionOverlay;
    private double _selectionStartX;
    private double _selectionStartY;

    /// <summary>Creates a new reader view model.</summary>
    public ReaderViewModel(
        IReaderSessionService sessions,
        IAnnotationService annotations,
        IBookmarkService bookmarks,
        IAnnotationLayerService layers,
        ICitationService citations,
        IReadingMemoryService readingMemory,
        ILocalizationService localization,
        ITextLayerService? textLayers = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(bookmarks);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(readingMemory);
        ArgumentNullException.ThrowIfNull(localization);

        _sessions = sessions;
        _annotations = annotations;
        _bookmarks = bookmarks;
        _layers = layers;
        _citations = citations;
        _readingMemory = readingMemory;
        _localization = localization;
        _textLayers = textLayers;

        _localization.CultureChanged += (_, _) => RaiseLocalizedProperties();
        RefreshBookmarkSortOptions();
        RefreshHighlightColorOptions();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The active book identity, or <see langword="null"/> when closed.</summary>
    public string? BookId
    {
        get => _bookId;
        private set
        {
            if (_bookId != value)
            {
                _bookId = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Current zero-based page index.</summary>
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPageNumber));
                OnPropertyChanged(nameof(PageStatusText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsCurrentPageBookmarked));
            }
        }
    }

    /// <summary>Current human-readable one-based page number.</summary>
    public int CurrentPageNumber => CurrentPageIndex + 1;

    /// <summary>Total pages in the active document.</summary>
    public int PageCount
    {
        get => _pageCount;
        private set
        {
            if (_pageCount != value)
            {
                _pageCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageStatusText));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    /// <summary>The active page rotation in PDF-standard degrees.</summary>
    public int PageRotationDegrees
    {
        get => _pageRotationDegrees;
        private set
        {
            int normalized = ((value % 360) + 360) % 360;
            if (_pageRotationDegrees != normalized)
            {
                _pageRotationDegrees = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageSurfaceWidth));
                OnPropertyChanged(nameof(PageSurfaceHeight));
            }
        }
    }

    /// <summary>The active reader zoom mode.</summary>
    public ZoomMode ZoomMode
    {
        get => _zoomMode;
        private set
        {
            if (_zoomMode != value)
            {
                _zoomMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverlayZoomFactor));
                OnPropertyChanged(nameof(PageSurfaceWidth));
                OnPropertyChanged(nameof(PageSurfaceHeight));
            }
        }
    }

    /// <summary>The active reader zoom percentage.</summary>
    public double ZoomPercent
    {
        get => _zoomPercent;
        private set
        {
            if (Math.Abs(_zoomPercent - value) > 0.001)
            {
                _zoomPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverlayZoomFactor));
                OnPropertyChanged(nameof(PageSurfaceWidth));
                OnPropertyChanged(nameof(PageSurfaceHeight));
            }
        }
    }

    /// <summary>True when a reader session is active.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True while opening or refreshing reader-side data.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Latest user-facing status message.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    /// <summary>True when <see cref="StatusMessage"/> has content.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>Currently selected reader sidebar tab.</summary>
    public int SelectedSidebarTabIndex
    {
        get => _selectedSidebarTabIndex;
        set => SetField(ref _selectedSidebarTabIndex, value);
    }

    /// <summary>Loaded bookmarks for the active book.</summary>
    public ObservableCollection<BookmarkListItem> Bookmarks { get; } = [];

    /// <summary>Available bookmark panel sort modes.</summary>
    public ObservableCollection<BookmarkSortOption> BookmarkSortOptions { get; } = [];

    /// <summary>The selected bookmark panel sort mode.</summary>
    public BookmarkSortOption? SelectedBookmarkSortOption
    {
        get => _selectedBookmarkSortOption;
        set
        {
            if (SetField(ref _selectedBookmarkSortOption, value))
            {
                SortBookmarks();
            }
        }
    }

    /// <summary>True when the current page already has a bookmark.</summary>
    public bool IsCurrentPageBookmarked => CurrentPageBookmark() is not null;

    /// <summary>Annotations on the current page.</summary>
    public ObservableCollection<AnnotationListItem> Annotations { get; } = [];

    /// <summary>The selected annotation in the annotation panel.</summary>
    public AnnotationListItem? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (!SetField(ref _selectedAnnotation, value) || value is not { IsNote: true })
            {
                return;
            }

            OpenNoteEditor(value);
        }
    }

    /// <summary>Overlay rectangles for annotations on the current page.</summary>
    public ObservableCollection<AnnotationOverlayItem> AnnotationOverlays { get; } = [];

    /// <summary>The active drag selection rectangle on the current page.</summary>
    public SelectionOverlayItem? SelectionOverlay
    {
        get => _selectionOverlay;
        private set
        {
            if (!EqualityComparer<SelectionOverlayItem?>.Default.Equals(_selectionOverlay, value))
            {
                _selectionOverlay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>True when the reader has an active page selection.</summary>
    public bool HasSelection => SelectionOverlay is not null;

    /// <summary>Loaded annotation layers for the active book.</summary>
    public ObservableCollection<LayerListItem> Layers { get; } = [];

    /// <summary>Layer filter choices for the annotation list and overlay.</summary>
    public ObservableCollection<LayerFilterOption> LayerFilterOptions { get; } = [];

    /// <summary>The selected layer filter for annotation display.</summary>
    public LayerFilterOption? SelectedLayerFilter
    {
        get => _selectedLayerFilter;
        set => SetField(ref _selectedLayerFilter, value);
    }

    /// <summary>Highlight color choices. The first option tracks the active layer color.</summary>
    public ObservableCollection<HighlightColorOption> HighlightColorOptions { get; } = [];

    /// <summary>The note currently open in the inline note editor.</summary>
    public AnnotationListItem? EditingNote
    {
        get => _editingNote;
        private set
        {
            if (!EqualityComparer<AnnotationListItem?>.Default.Equals(_editingNote, value))
            {
                _editingNote = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNoteEditorOpen));
            }
        }
    }

    /// <summary>Editable text for the open note editor.</summary>
    public string? EditingNoteText
    {
        get => _editingNoteText;
        set => SetField(ref _editingNoteText, value);
    }

    /// <summary>True when a note is open in the inline note editor.</summary>
    public bool IsNoteEditorOpen => EditingNote is not null;

    /// <summary>The annotation waiting for delete confirmation.</summary>
    public AnnotationListItem? PendingDeleteAnnotation
    {
        get => _pendingDeleteAnnotation;
        private set
        {
            if (!EqualityComparer<AnnotationListItem?>.Default.Equals(_pendingDeleteAnnotation, value))
            {
                _pendingDeleteAnnotation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPendingDeleteAnnotation));
                OnPropertyChanged(nameof(DeleteAnnotationConfirmationText));
            }
        }
    }

    /// <summary>True when the delete confirmation panel should be visible.</summary>
    public bool HasPendingDeleteAnnotation => PendingDeleteAnnotation is not null;

    /// <summary>Why the reader opened this book.</summary>
    public string? OpenedBecause
    {
        get => _openedBecause;
        set => SetField(ref _openedBecause, value);
    }

    /// <summary>The reader's key insight.</summary>
    public string? KeyInsight
    {
        get => _keyInsight;
        set => SetField(ref _keyInsight, value);
    }

    /// <summary>Questions left open by this reading session.</summary>
    public string? OpenQuestions
    {
        get => _openQuestions;
        set => SetField(ref _openQuestions, value);
    }

    /// <summary>Disposition as editable text; valid values are 1 through 5.</summary>
    public string? DispositionText
    {
        get => _dispositionText;
        set => SetField(ref _dispositionText, value);
    }

    /// <summary>The current selected text passage, supplied by the reader selection surface.</summary>
    public string? SelectedCitationText
    {
        get => _selectedCitationText;
        set => SetField(ref _selectedCitationText, value);
    }

    /// <summary>The currently captured citation card, or <see langword="null"/> when closed.</summary>
    public CitationCardItem? CitationCard
    {
        get => _citationCard;
        private set
        {
            if (!EqualityComparer<CitationCardItem?>.Default.Equals(_citationCard, value))
            {
                _citationCard = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCitationCard));
                OnPropertyChanged(nameof(CitationPlainText));
            }
        }
    }

    /// <summary>True when a citation card is open.</summary>
    public bool HasCitationCard => CitationCard is not null;

    /// <summary>The currently captured citation formatted for clipboard/export.</summary>
    public string CitationPlainText => CitationCard?.PlainText ?? string.Empty;

    /// <summary>Formatted page status.</summary>
    public string PageStatusText => PageCount <= 0
        ? string.Empty
        : string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Reader.Navigation.PageOf"],
            CurrentPageNumber,
            PageCount);

    /// <summary>True when the reader can navigate to the previous page.</summary>
    public bool CanGoPrevious => IsOpen && CurrentPageIndex > 0;

    /// <summary>True when the reader can navigate to the next page.</summary>
    public bool CanGoNext => IsOpen && CurrentPageIndex < PageCount - 1;

    /// <summary>Localized label for moving to the previous page.</summary>
    public string PreviousPageLabel => _localization["Reader.Navigation.PreviousPage"];

    /// <summary>Localized label for moving to the next page.</summary>
    public string NextPageLabel => _localization["Reader.Navigation.NextPage"];

    /// <summary>The page-surface width after rotation and fixed zoom are applied.</summary>
    public double PageSurfaceWidth =>
        (PageRotationDegrees is 90 or 270 ? BasePageSurfaceHeight : BasePageSurfaceWidth) * OverlayZoomFactor;

    /// <summary>The page-surface height after rotation and fixed zoom are applied.</summary>
    public double PageSurfaceHeight =>
        (PageRotationDegrees is 90 or 270 ? BasePageSurfaceWidth : BasePageSurfaceHeight) * OverlayZoomFactor;

    /// <summary>The effective zoom factor used by annotation overlay rendering.</summary>
    public double OverlayZoomFactor => ZoomMode == ZoomMode.Fixed
        ? Math.Max(0.25, ZoomPercent / 100.0)
        : 1.0;

    /// <summary>Localized label for the reader panel.</summary>
    public string ReaderTitle => _localization["Reader.Panel.Title"];

    /// <summary>Localized label for the bookmark panel.</summary>
    public string BookmarkPanelLabel => _localization["Bookmark.Panel"];

    /// <summary>Localized label for the bookmark sort selector.</summary>
    public string BookmarkSortLabel => _localization["Bookmark.Sort.Label"];

    /// <summary>Screen-reader label for the bookmark panel including item count.</summary>
    public string BookmarkPanelAccessibleLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Bookmark.Panel.AccessibleFormat"],
        BookmarkPanelLabel,
        Bookmarks.Count);

    /// <summary>Localized label for the annotations panel.</summary>
    public string AnnotationPanelLabel => _localization["Annotation.Panel"];

    /// <summary>Localized label for creating a highlight.</summary>
    public string CreateHighlightLabel => _localization["Annotation.Highlight.Create"];

    /// <summary>Localized label for the highlight color picker.</summary>
    public string ChooseHighlightColorLabel => _localization["Annotation.Highlight.Color"];

    /// <summary>Localized label for creating a note.</summary>
    public string CreateNoteLabel => _localization["Annotation.Note.Create"];

    /// <summary>Localized label for deleting an annotation.</summary>
    public string DeleteAnnotationLabel => _localization["Annotation.Delete"];

    /// <summary>Localized label for confirming annotation deletion.</summary>
    public string ConfirmDeleteAnnotationLabel => _localization["Annotation.Delete.Confirm"];

    /// <summary>Localized label for cancelling annotation deletion.</summary>
    public string CancelDeleteAnnotationLabel => _localization["Annotation.Delete.Cancel"];

    /// <summary>Localized delete confirmation text.</summary>
    public string DeleteAnnotationConfirmationText => PendingDeleteAnnotation is null
        ? string.Empty
        : string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Annotation.Delete.Confirmation"],
            PendingDeleteAnnotation.Kind);

    /// <summary>Localized label for editing a note.</summary>
    public string EditNoteLabel => _localization["Annotation.Note.Edit"];

    /// <summary>Localized label for the inline note editor.</summary>
    public string NoteEditorLabel => _localization["Annotation.Note.Editor"];

    /// <summary>Localized label for adding a bookmark.</summary>
    public string AddBookmarkLabel => _localization["Bookmark.Add"];

    /// <summary>Localized label for removing a bookmark.</summary>
    public string RemoveBookmarkLabel => _localization["Bookmark.Remove"];

    /// <summary>Localized label for renaming a bookmark.</summary>
    public string RenameBookmarkLabel => _localization["Bookmark.Rename"];

    /// <summary>Localized label for the layer panel.</summary>
    public string LayerPanelLabel => _localization["Layer.Panel"];

    /// <summary>Localized label for filtering annotations by layer.</summary>
    public string LayerFilterLabel => _localization["Layer.Filter"];

    /// <summary>Localized label for adding a layer.</summary>
    public string AddLayerLabel => _localization["Layer.Add"];

    /// <summary>Localized label for merging a layer.</summary>
    public string MergeLayerLabel => _localization["Layer.Merge"];

    /// <summary>Localized label for toggling layer visibility.</summary>
    public string LayerVisibilityLabel => _localization["Layer.Visible"];

    /// <summary>Localized label for deleting a layer.</summary>
    public string DeleteLayerLabel => _localization["Layer.Delete"];

    /// <summary>Localized label for reading memory.</summary>
    public string ReadingMemoryLabel => _localization["ReadingMemory.Open"];

    /// <summary>Localized label for the opened-because field.</summary>
    public string OpenedBecauseLabel => _localization["ReadingMemory.OpenedBecause"];

    /// <summary>Localized label for the key-insight field.</summary>
    public string KeyInsightLabel => _localization["ReadingMemory.KeyInsight"];

    /// <summary>Localized label for open questions.</summary>
    public string OpenQuestionsLabel => _localization["ReadingMemory.OpenQuestions"];

    /// <summary>Localized label for disposition.</summary>
    public string DispositionLabel => _localization["ReadingMemory.Disposition"];

    /// <summary>Localized label for saving reading memory.</summary>
    public string SaveReadingMemoryLabel => _localization["ReadingMemory.Save"];

    /// <summary>Localized label for capturing a citation.</summary>
    public string CaptureCitationLabel => _localization["Citation.Capture"];

    /// <summary>The effective color used by the next highlight.</summary>
    public string SelectedHighlightColor => _selectedHighlightColorOverride ?? CurrentLayerHighlightColor();

    /// <summary>Localized label for copying a citation.</summary>
    public string CopyCitationLabel => _localization["Citation.Copy"];

    /// <summary>Localized label for exporting a citation.</summary>
    public string ExportCitationLabel => _localization["Citation.Export"];

    /// <summary>Localized label for closing a citation card.</summary>
    public string CloseCitationLabel => _localization["Icon.ic_close.Label"];

    /// <summary>Opens a reader session and refreshes all Phase 09 side-panel data.</summary>
    public async Task OpenAsync(string bookId, int? pageHint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        IsBusy = true;
        StatusMessage = null;

        try
        {
            ReaderSession session = await _sessions
                .OpenAsync(bookId, pageHint, cancellationToken)
                .ConfigureAwait(true);

            await UpdateSessionAsync(session, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            StatusMessage = _localization["Reader.Error.FileNotFound"];
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Navigates to the previous page, if available.</summary>
    public Task GoPreviousAsync() => NavigateToAsync(CurrentPageIndex - 1);

    /// <summary>Navigates to the next page, if available.</summary>
    public Task GoNextAsync() => NavigateToAsync(CurrentPageIndex + 1);

    /// <summary>Navigates to the page referenced by a bookmark.</summary>
    public Task NavigateToBookmarkAsync(BookmarkListItem bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return NavigateToAsync(bookmark.PageIndex);
    }

    /// <summary>Adds a bookmark for the current page and refreshes the bookmark list.</summary>
    public async Task AddBookmarkAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            CancelDeleteAnnotation();
            return;
        }

        if (IsCurrentPageBookmarked)
        {
            StatusMessage = _localization["Bookmark.Saved"];
            return;
        }

        await _bookmarks
            .CreateAsync(BookId, CurrentPageIndex, DefaultBookmarkLabel(CurrentPageIndex), cancellationToken)
            .ConfigureAwait(true);

        await RefreshBookmarksAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Bookmark.Saved"];
    }

    /// <summary>Adds or removes the bookmark on the current page.</summary>
    public async Task ToggleBookmarkAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null || _isTogglingBookmark)
        {
            return;
        }

        _isTogglingBookmark = true;
        try
        {
            BookmarkListItem? existing = CurrentPageBookmark();
            if (existing is null)
            {
                await AddBookmarkAsync(cancellationToken).ConfigureAwait(true);
                return;
            }

            await _bookmarks
                .DeleteAsync(existing.Id, cancellationToken)
                .ConfigureAwait(true);

            await RefreshBookmarksAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = _localization["Bookmark.Removed"];
        }
        finally
        {
            _isTogglingBookmark = false;
        }
    }

    /// <summary>Selects the bookmark panel in the reader sidebar.</summary>
    public void OpenBookmarkPanel() => SelectedSidebarTabIndex = BookmarkTabIndex;

    /// <summary>Renames a bookmark from the inline bookmark panel editor.</summary>
    public async Task RenameBookmarkAsync(
        BookmarkListItem bookmark,
        string? newLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        string effectiveLabel = string.IsNullOrWhiteSpace(newLabel)
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Bookmark.DefaultLabelFormat"],
                bookmark.PageIndex + 1)
            : newLabel.Trim();

        if (string.Equals(bookmark.PersistedLabel, effectiveLabel, StringComparison.Ordinal))
        {
            return;
        }

        await _bookmarks
            .RenameAsync(bookmark.Id, effectiveLabel, cancellationToken)
            .ConfigureAwait(true);

        bookmark.Label = effectiveLabel;
        bookmark.MarkPersistedLabel(effectiveLabel);
        await RefreshBookmarksAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Bookmark.Renamed"];
    }

    /// <summary>Deletes a bookmark from the bookmark panel.</summary>
    public async Task DeleteBookmarkAsync(
        BookmarkListItem bookmark,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        await _bookmarks
            .DeleteAsync(bookmark.Id, cancellationToken)
            .ConfigureAwait(true);

        await RefreshBookmarksAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Bookmark.Removed"];
    }

    /// <summary>Creates a highlight on the current page and refreshes the overlay.</summary>
    public async Task CreateHighlightAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            return;
        }

        LayerListItem? layer = ActiveWritableLayer();
        string color = SelectedHighlightColor;
        string? layerId = layer?.Id;
        string quote = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Annotation.SampleQuoteFormat"],
            CurrentPageNumber);

        await _annotations
            .CreateHighlightAsync(
                BookId,
                layerId,
                [DefaultAnnotationRegion()],
                color,
                quote,
                cancellationToken)
            .ConfigureAwait(true);

        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Saved"];
    }

    /// <summary>Creates a highlight from the current page selection.</summary>
    public async Task CreateHighlightFromSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null || SelectionOverlay is not { } selection)
        {
            return;
        }

        LayerListItem? layer = ActiveWritableLayer();
        string quote = await SelectedCitationTextFromSelectionAsync(cancellationToken)
            .ConfigureAwait(true);

        await _annotations
            .CreateHighlightAsync(
                BookId,
                layer?.Id,
                TextSelectionService.GetRegionsForSelection(
                    CurrentPageIndex,
                    SelectionRect(selection),
                    BasePageSurfaceWidth,
                    BasePageSurfaceHeight,
                    OverlayZoomFactor,
                    PageRotationDegrees),
                SelectedHighlightColor,
                quote,
                cancellationToken)
            .ConfigureAwait(true);

        ClearSelection();
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Saved"];
    }

    /// <summary>Selects the color used by newly created highlights.</summary>
    public void SelectHighlightColor(HighlightColorOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        _selectedHighlightColorOverride = option.IsLayerDefault ? null : option.Color;
        RefreshHighlightColorOptions();
        OnPropertyChanged(nameof(SelectedHighlightColor));
    }

    /// <summary>Creates a note on the current page and refreshes the overlay.</summary>
    public async Task CreateNoteAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            return;
        }

        string? layerId = ActiveWritableLayer()?.Id;
        string note = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Annotation.SampleNoteFormat"],
            CurrentPageNumber);

        await _annotations
            .CreateNoteAsync(
                BookId,
                layerId,
                DefaultAnnotationRegion(),
                note,
                cancellationToken)
            .ConfigureAwait(true);

        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Saved"];
    }

    /// <summary>Creates a note anchored to the current page selection.</summary>
    public async Task CreateNoteFromSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null || SelectionOverlay is not { } selection)
        {
            return;
        }

        string note = await SelectedCitationTextFromSelectionAsync(cancellationToken)
            .ConfigureAwait(true);

        await _annotations
            .CreateNoteAsync(
                BookId,
                ActiveWritableLayer()?.Id,
                SelectionRegion(selection),
                note,
                cancellationToken)
            .ConfigureAwait(true);

        ClearSelection();
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Saved"];
    }

    /// <summary>Deletes an annotation from the current page and refreshes overlays.</summary>
    public async Task DeleteAnnotationAsync(
        AnnotationListItem annotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (BookId is null)
        {
            return;
        }

        await _annotations
            .DeleteAsync(annotation.Id, cancellationToken)
            .ConfigureAwait(true);

        if (EditingNote?.Id == annotation.Id)
        {
            CloseNoteEditor();
        }

        if (PendingDeleteAnnotation?.Id == annotation.Id)
        {
            CancelDeleteAnnotation();
        }

        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Deleted"];
    }

    /// <summary>Requests confirmation before deleting an annotation.</summary>
    public void RequestDeleteAnnotation(AnnotationListItem annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        CloseNoteEditor();
        PendingDeleteAnnotation = annotation;
    }

    /// <summary>Cancels the pending annotation delete request.</summary>
    public void CancelDeleteAnnotation() => PendingDeleteAnnotation = null;

    /// <summary>Confirms and performs the pending annotation delete request.</summary>
    public async Task ConfirmDeleteAnnotationAsync(CancellationToken cancellationToken = default)
    {
        if (PendingDeleteAnnotation is not { } annotation)
        {
            return;
        }

        await DeleteAnnotationAsync(annotation, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opens the inline note editor for a note annotation.</summary>
    public void OpenNoteEditor(AnnotationListItem annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (!annotation.IsNote)
        {
            return;
        }

        EditingNote = annotation;
        EditingNoteText = annotation.NoteText;
    }

    /// <summary>Opens the inline note editor for a note annotation by identifier.</summary>
    public bool OpenNoteEditorById(string annotationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);

        AnnotationListItem? annotation = Annotations
            .FirstOrDefault(item => string.Equals(item.Id, annotationId, StringComparison.Ordinal));

        if (annotation is not { IsNote: true })
        {
            return false;
        }

        OpenNoteEditor(annotation);
        return true;
    }

    /// <summary>Closes the inline note editor without persisting additional changes.</summary>
    public void CloseNoteEditor()
    {
        EditingNote = null;
        EditingNoteText = null;
    }

    /// <summary>Saves the open note editor text and refreshes the current page annotations.</summary>
    public async Task SaveOpenNoteAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null || EditingNote is not { } note)
        {
            return;
        }

        if (!_annotationsById.TryGetValue(note.Id, out AnnotationV2? annotation) ||
            annotation.Kind != AnnotationKind.Note)
        {
            CloseNoteEditor();
            return;
        }

        string newText = EditingNoteText?.Trim() ?? string.Empty;

        if (string.Equals(annotation.NoteText, newText, StringComparison.Ordinal))
        {
            CloseNoteEditor();
            return;
        }

        annotation.NoteText = newText;
        await _annotations.UpdateAsync(annotation, cancellationToken).ConfigureAwait(true);
        CloseNoteEditor();
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Annotation.Note.Saved"];
    }

    /// <summary>Creates a new annotation layer with the next default colour.</summary>
    public async Task AddLayerAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            return;
        }

        string name = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Layer.DefaultNameFormat"],
            Layers.Count + 1);

        await _layers
            .CreateLayerAsync(BookId, name, NextLayerColor(), cancellationToken)
            .ConfigureAwait(true);

        await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Updates a layer visibility flag and refreshes filtered annotations.</summary>
    public async Task SetLayerVisibilityAsync(
        LayerListItem layer,
        bool isVisible,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (BookId is null || layer.IsVisible == isVisible)
        {
            return;
        }

        await _layers
            .SetVisibilityAsync(layer.Id, isVisible, cancellationToken)
            .ConfigureAwait(true);

        await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Renames an annotation layer from the inline layer panel editor.</summary>
    public async Task RenameLayerAsync(
        LayerListItem layer,
        string? newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layer);

        string effectiveName = string.IsNullOrWhiteSpace(newName)
            ? layer.Name
            : newName.Trim();

        if (string.Equals(layer.PersistedName, effectiveName, StringComparison.Ordinal))
        {
            return;
        }

        await _layers
            .RenameLayerAsync(layer.Id, effectiveName, cancellationToken)
            .ConfigureAwait(true);

        layer.Name = effectiveName;
        layer.MarkPersistedName(effectiveName);
        await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = _localization["Layer.Renamed"];
    }

    /// <summary>Deletes an annotation layer when more than one layer remains.</summary>
    public async Task DeleteLayerAsync(
        LayerListItem layer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (BookId is null)
        {
            return;
        }

        try
        {
            await _layers
                .DeleteAsync(BookId, layer.Id, cancellationToken)
                .ConfigureAwait(true);

            await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
            await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = _localization["Layer.Deleted"];
        }
        catch (InvalidOperationException)
        {
            StatusMessage = _localization["Layer.AtLeastOne"];
        }
    }

    /// <summary>Merges a layer into the first other available layer.</summary>
    public async Task MergeLayerIntoFirstAvailableAsync(
        LayerListItem layer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (BookId is null)
        {
            return;
        }

        LayerListItem? target =
            Layers.FirstOrDefault(candidate => candidate.Id != layer.Id && candidate.IsVisible) ??
            Layers.FirstOrDefault(candidate => candidate.Id != layer.Id);
        if (target is null)
        {
            StatusMessage = _localization["Layer.AtLeastOne"];
            return;
        }

        try
        {
            await _layers
                .MergeLayersAsync(BookId, layer.Id, target.Id, cancellationToken)
                .ConfigureAwait(true);

            await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
            await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = _localization["Layer.Merged"];
        }
        catch (InvalidOperationException)
        {
            StatusMessage = _localization["Layer.AtLeastOne"];
        }
    }

    /// <summary>Updates the annotation layer filter and refreshes displayed annotations.</summary>
    public async Task SelectLayerFilterAsync(
        LayerFilterOption? option,
        CancellationToken cancellationToken = default)
    {
        if (EqualityComparer<LayerFilterOption?>.Default.Equals(_selectedLayerFilter, option))
        {
            return;
        }

        _selectedLayerFilter = option;
        OnPropertyChanged(nameof(SelectedLayerFilter));
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Captures a citation card from the current selected passage.</summary>
    public async Task CaptureCitationAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCitationText))
        {
            StatusMessage = _localization["Citation.NoSelection"];
            return;
        }

        CitationCard card = await _citations
            .CaptureAsync(BookId, CurrentPageIndex, SelectedCitationText.Trim(), cancellationToken)
            .ConfigureAwait(true);

        _currentCitationCard = card;
        CitationCard = CitationCardItem.From(
            card,
            _localization["Citation.UnknownTitle"],
            _localization["Citation.UnknownAuthor"],
            _localization["Citation.PageFormat"]);
        StatusMessage = null;
    }

    /// <summary>Captures a citation from the current page selection.</summary>
    public async Task CaptureCitationFromSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectionOverlay is null)
        {
            StatusMessage = _localization["Citation.NoSelection"];
            return;
        }

        SelectedCitationText = await SelectedCitationTextFromSelectionAsync(cancellationToken)
            .ConfigureAwait(true);
        await CaptureCitationAsync(cancellationToken).ConfigureAwait(true);
        ClearSelection();
    }

    /// <summary>Closes the current citation card.</summary>
    public void CloseCitationCard()
    {
        _currentCitationCard = null;
        CitationCard = null;
    }

    /// <summary>Marks the current citation as copied/exported for the reader status bar.</summary>
    public void MarkCitationCopied() => StatusMessage = _localization["Citation.Copied"];

    /// <summary>Exports the current citation card to a plain-text sidecar file.</summary>
    public async Task<string?> ExportCitationAsync(CancellationToken cancellationToken = default)
    {
        if (_currentCitationCard is null)
        {
            return null;
        }

        string path = await _citations.ExportAsync(_currentCitationCard, cancellationToken)
            .ConfigureAwait(true);
        StatusMessage = _localization["Citation.Exported"];
        return path;
    }

    /// <summary>Schedules a debounced reading-memory save after a field loses focus.</summary>
    public async Task AutoSaveReadingMemoryAsync(
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        _readingMemoryAutoSaveCts?.Cancel();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readingMemoryAutoSaveCts = cts;

        try
        {
            await Task.Delay(delay ?? TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(true);

            if (!cts.IsCancellationRequested)
            {
                await SaveReadingMemoryAsync(cts.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_readingMemoryAutoSaveCts, cts))
            {
                _readingMemoryAutoSaveCts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>Saves the reading-memory fields for the active book.</summary>
    public async Task SaveReadingMemoryAsync(CancellationToken cancellationToken = default)
    {
        if (BookId is null)
        {
            return;
        }

        int? disposition = null;
        if (!string.IsNullOrWhiteSpace(DispositionText))
        {
            if (!int.TryParse(
                    DispositionText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out int parsed))
            {
                StatusMessage = _localization["ReadingMemory.InvalidDisposition"];
                return;
            }

            disposition = parsed;
        }

        var memory = new ReadingMemory
        {
            BookId = BookId,
            OpenedBecause = OpenedBecause,
            KeyInsight = KeyInsight,
            OpenQuestions = OpenQuestions,
            Disposition = disposition,
        };

        try
        {
            await _readingMemory.SaveAsync(memory, cancellationToken).ConfigureAwait(true);
            StatusMessage = _localization["ReadingMemory.Saved"];
        }
        catch (ArgumentOutOfRangeException)
        {
            StatusMessage = _localization["ReadingMemory.InvalidDisposition"];
        }
    }

    private async Task NavigateToAsync(int pageIndex)
    {
        if (!IsOpen || pageIndex < 0 || pageIndex >= PageCount)
        {
            return;
        }

        await _sessions.NavigateToAsync(pageIndex).ConfigureAwait(true);
        ReaderSession? current = _sessions.CurrentSession;
        if (current is not null)
        {
            ApplySession(current);
            await RefreshAnnotationsAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async Task UpdateSessionAsync(ReaderSession session, CancellationToken cancellationToken)
    {
        ApplySession(session);
        await EnsureDefaultLayerAsync(cancellationToken).ConfigureAwait(true);
        await RefreshLayersAsync(cancellationToken).ConfigureAwait(true);
        await RefreshAnnotationsAsync(cancellationToken).ConfigureAwait(true);
        await RefreshBookmarksAsync(cancellationToken).ConfigureAwait(true);
        await RefreshReadingMemoryAsync(cancellationToken).ConfigureAwait(true);
    }

    private void ApplySession(ReaderSession session)
    {
        BookId = session.BookId;
        PageCount = session.PageCount;
        CurrentPageIndex = session.CurrentPageIndex;
        PageRotationDegrees = session.PageRotationDegrees;
        ZoomMode = session.ZoomMode;
        ZoomPercent = session.ZoomPercent;
        IsOpen = true;
    }

    private async Task EnsureDefaultLayerAsync(CancellationToken cancellationToken)
    {
        if (BookId is null)
        {
            return;
        }

        IReadOnlyList<AnnotationLayer> existing = await _layers
            .GetLayersAsync(BookId, cancellationToken)
            .ConfigureAwait(true);

        if (existing.Count > 0)
        {
            return;
        }

        await _layers
            .CreateLayerAsync(
                BookId,
                _localization["Layer.DefaultName"],
                "#FFCC66",
                cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task RefreshBookmarksAsync(CancellationToken cancellationToken)
    {
        Bookmarks.Clear();
        if (BookId is null)
        {
            return;
        }

        IReadOnlyList<Bookmark> items = await _bookmarks
            .GetForBookAsync(BookId, cancellationToken)
            .ConfigureAwait(true);

        foreach (Bookmark bookmark in items)
        {
            Bookmarks.Add(new BookmarkListItem(
                bookmark.Id,
                bookmark.PageIndex,
                bookmark.Label ?? DefaultBookmarkLabel(bookmark.PageIndex),
                bookmark.CreatedUtc,
                _localization["Bookmark.Item.AccessibleFormat"]));
        }

        SortBookmarks();
        OnPropertyChanged(nameof(IsCurrentPageBookmarked));
        OnPropertyChanged(nameof(BookmarkPanelAccessibleLabel));
    }

    private string DefaultBookmarkLabel(int pageIndex) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Bookmark.DefaultLabelFormat"],
            pageIndex + 1);

    private async Task RefreshAnnotationsAsync(CancellationToken cancellationToken)
    {
        Annotations.Clear();
        AnnotationOverlays.Clear();
        _annotationsById.Clear();

        if (BookId is null)
        {
            return;
        }

        IReadOnlyList<AnnotationV2> items = await _annotations
            .GetForPageAsync(BookId, CurrentPageIndex, cancellationToken)
            .ConfigureAwait(true);

        string? defaultLayerId = Layers.FirstOrDefault()?.Id;
        HashSet<string> visibleLayerIds = Layers
            .Where(layer => layer.IsVisible)
            .Select(layer => layer.Id)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> layerNames = Layers.ToDictionary(
            layer => layer.Id,
            layer => layer.Name,
            StringComparer.Ordinal);

        foreach (AnnotationV2 annotation in items)
        {
            _annotationsById[annotation.Id] = annotation;

            string? effectiveLayerId = annotation.LayerId ?? defaultLayerId;
            if (!IsAnnotationVisible(effectiveLayerId, visibleLayerIds) ||
                !IsAnnotationInSelectedFilter(effectiveLayerId))
            {
                continue;
            }

            bool isNote = annotation.Kind == AnnotationKind.Note;
            string label = annotation.Kind == AnnotationKind.Highlight
                ? _localization["Annotation.Highlight.Create"]
                : _localization["Annotation.Note.Anchor"];

            string preview = isNote
                ? annotation.NoteText ?? string.Empty
                : annotation.QuoteText ?? string.Empty;
            string accessibleLabel;
            string noteAnchorAccessibleLabel;
            if (annotation.LayerId is not null &&
                layerNames.TryGetValue(annotation.LayerId, out string? layerName))
            {
                accessibleLabel = AnnotationAccessibleLabel(label, layerName, PageStatusText);
                noteAnchorAccessibleLabel = AnnotationAccessibleLabel(
                    _localization["Annotation.Note.AnchorMarker"],
                    layerName,
                    PageStatusText);
            }
            else if (annotation.LayerId is null &&
                defaultLayerId is not null &&
                layerNames.TryGetValue(defaultLayerId, out string? defaultLayerName))
            {
                accessibleLabel = AnnotationAccessibleLabel(label, defaultLayerName, PageStatusText);
                noteAnchorAccessibleLabel = AnnotationAccessibleLabel(
                    _localization["Annotation.Note.AnchorMarker"],
                    defaultLayerName,
                    PageStatusText);
            }
            else
            {
                accessibleLabel = AnnotationAccessibleLabel(label, null, PageStatusText);
                noteAnchorAccessibleLabel = AnnotationAccessibleLabel(
                    _localization["Annotation.Note.AnchorMarker"],
                    null,
                    PageStatusText);
            }

            Annotations.Add(new AnnotationListItem(
                annotation.Id,
                label,
                preview,
                annotation.LayerId,
                annotation.HighlightColor ?? "#FFCC66",
                isNote,
                annotation.NoteText));

            foreach (AnnotationRegion region in annotation.Regions.Where(r => r.PageIndex == CurrentPageIndex))
            {
                ScreenRect rect = AnnotationRenderHelper.ToScreenRect(
                    region,
                    BasePageSurfaceWidth,
                    BasePageSurfaceHeight,
                    PageRotationDegrees,
                    OverlayZoomFactor);

                AnnotationOverlays.Add(new AnnotationOverlayItem(
                    annotation.Id,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    annotation.HighlightColor ?? "#FFCC66",
                    accessibleLabel,
                    noteAnchorAccessibleLabel,
                    isNote));
            }
        }

        if (PendingDeleteAnnotation is not null &&
            !Annotations.Any(annotation => annotation.Id == PendingDeleteAnnotation.Id))
        {
            CancelDeleteAnnotation();
        }
    }

    private string AnnotationAccessibleLabel(string label, string? layerName, string pageStatus) =>
        string.IsNullOrWhiteSpace(layerName)
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Annotation.AccessibleLabelWithoutLayerFormat"],
                label,
                pageStatus)
            : string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Annotation.AccessibleLabelWithLayerFormat"],
                label,
                layerName,
                pageStatus);

    private async Task RefreshLayersAsync(CancellationToken cancellationToken)
    {
        Layers.Clear();
        if (BookId is null)
        {
            return;
        }

        IReadOnlyList<AnnotationLayer> items = await _layers
            .GetLayersAsync(BookId, cancellationToken)
            .ConfigureAwait(true);
        bool canMergeOrDelete = items.Count > 1;
        string? activeLayerId = items.FirstOrDefault(layer => layer.IsVisible)?.Id;

        foreach (AnnotationLayer layer in items)
        {
            Layers.Add(new LayerListItem(
                layer.Id,
                layer.Name,
                layer.Color,
                layer.IsVisible,
                string.Equals(layer.Id, activeLayerId, StringComparison.Ordinal),
                canMergeOrDelete,
                _localization["Layer.Active"],
                _localization["Layer.Active.AccessibleFormat"],
                _localization["Layer.Visible.AccessibleFormat"],
                _localization["Layer.Hidden.AccessibleFormat"],
                _localization["Layer.Merge.AccessibleFormat"],
                _localization["Layer.Delete.AccessibleFormat"]));
        }

        RefreshLayerFilterOptions();
        RefreshHighlightColorOptions();
    }

    private async Task RefreshReadingMemoryAsync(CancellationToken cancellationToken)
    {
        if (BookId is null)
        {
            return;
        }

        ReadingMemory memory = await _readingMemory
            .LoadAsync(BookId, cancellationToken)
            .ConfigureAwait(true);

        OpenedBecause = memory.OpenedBecause;
        KeyInsight = memory.KeyInsight;
        OpenQuestions = memory.OpenQuestions;
        DispositionText = memory.Disposition?.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    private string NextLayerColor()
    {
        string[] palette = ["#FFCC66", "#88AA77", "#C7795A", "#8E5A8A"];
        return palette[Layers.Count % palette.Length];
    }

    private LayerListItem? ActiveWritableLayer() =>
        Layers.FirstOrDefault(layer => layer.IsVisible);

    private string CurrentLayerHighlightColor() => ActiveWritableLayer()?.Color ?? "#FFCC66";

    /// <summary>Starts a drag selection on the page surface.</summary>
    public void BeginSelection(double x, double y)
    {
        _selectionStartX = Clamp(x, 0, PageSurfaceWidth);
        _selectionStartY = Clamp(y, 0, PageSurfaceHeight);
        SelectionOverlay = new SelectionOverlayItem(_selectionStartX, _selectionStartY, 0, 0);
        StatusMessage = null;
    }

    /// <summary>Updates the current drag selection rectangle.</summary>
    public void UpdateSelection(double x, double y)
    {
        if (SelectionOverlay is null)
        {
            return;
        }

        double currentX = Clamp(x, 0, PageSurfaceWidth);
        double currentY = Clamp(y, 0, PageSurfaceHeight);
        double left = Math.Min(_selectionStartX, currentX);
        double top = Math.Min(_selectionStartY, currentY);
        double width = Math.Abs(currentX - _selectionStartX);
        double height = Math.Abs(currentY - _selectionStartY);
        SelectionOverlay = new SelectionOverlayItem(left, top, width, height);
    }

    /// <summary>Finishes selection and opens the selection action menu when meaningful.</summary>
    public void CompleteSelection()
    {
        if (SelectionOverlay is not { Width: >= 4, Height: >= 4 })
        {
            ClearSelection();
        }
    }

    /// <summary>Clears the current page selection.</summary>
    public void ClearSelection() => SelectionOverlay = null;

    private AnnotationRegion SelectionRegion(SelectionOverlayItem selection)
    {
        return TextSelectionService
            .GetRegionsForSelection(
                CurrentPageIndex,
                SelectionRect(selection),
                BasePageSurfaceWidth,
                BasePageSurfaceHeight,
                OverlayZoomFactor,
                PageRotationDegrees)
            .Single();
    }

    private static Rect SelectionRect(SelectionOverlayItem selection) =>
        new(selection.X, selection.Y, selection.Width, selection.Height);

    private async Task<string> SelectedCitationTextFromSelectionAsync(CancellationToken cancellationToken)
    {
        if (BookId is not null &&
            SelectionOverlay is { } selection &&
            _textLayers is not null)
        {
            AnnotationRegion region = SelectionRegion(selection);

            try
            {
                TextLayer layer = await _textLayers
                    .ExtractAsync(BookId, CurrentPageIndex, cancellationToken)
                    .ConfigureAwait(true);
                string selectedText = ExtractWordsInRegion(layer, region);
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    return selectedText;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Selection text extraction should not block annotation capture.
            catch (Exception)
            {
                // Fall through to the deterministic placeholder text.
            }
#pragma warning restore CA1031
        }

        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Annotation.SelectionQuoteFormat"],
            CurrentPageNumber);
    }

    private static string ExtractWordsInRegion(TextLayer layer, AnnotationRegion region)
    {
        double right = region.NormLeft + region.NormWidth;
        double bottom = region.NormTop + region.NormHeight;

        return string.Join(
            " ",
            layer.Words
                .Where(word =>
                    word.Right > region.NormLeft &&
                    word.Left < right &&
                    word.Bottom > region.NormTop &&
                    word.Top < bottom)
                .OrderBy(word => word.Top)
                .ThenBy(word => word.Left)
                .Select(word => word.Text)
                .Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private void RefreshHighlightColorOptions()
    {
        HighlightColorOptions.Clear();

        string layerColor = CurrentLayerHighlightColor();
        HighlightColorOptions.Add(new HighlightColorOption(
            LayerDefaultColorOptionId,
            layerColor,
            _localization["Annotation.Highlight.LayerColor"],
            HighlightColorAccessibleLabel(
                _localization["Annotation.Highlight.LayerColor"],
                layerColor,
                isSelected: _selectedHighlightColorOverride is null),
            IsLayerDefault: true,
            IsSelected: _selectedHighlightColorOverride is null));

        string[] overrides = ["#FFCC66", "#88AA77", "#C7795A", "#8E5A8A"];
        foreach (string color in overrides)
        {
            HighlightColorOptions.Add(new HighlightColorOption(
                color,
                color,
                color,
                HighlightColorAccessibleLabel(
                    _localization["Annotation.Highlight.Color"],
                    color,
                    isSelected: string.Equals(
                        _selectedHighlightColorOverride,
                        color,
                        StringComparison.OrdinalIgnoreCase)),
                IsLayerDefault: false,
                IsSelected: string.Equals(
                    _selectedHighlightColorOverride,
                    color,
                    StringComparison.OrdinalIgnoreCase)));
        }

        OnPropertyChanged(nameof(HighlightColorOptions));
        OnPropertyChanged(nameof(SelectedHighlightColor));
    }

    private string HighlightColorAccessibleLabel(string label, string color, bool isSelected) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Annotation.Highlight.ColorOptionFormat"],
            label,
            color,
            isSelected ? _localization["Annotation.Highlight.ColorSelectedSuffix"] : string.Empty);

    private void RefreshLayerFilterOptions()
    {
        string selectedId = SelectedLayerFilter?.Id ?? AllVisibleLayersFilterId;

        LayerFilterOptions.Clear();

        var allVisible = new LayerFilterOption(
            AllVisibleLayersFilterId,
            _localization["Layer.Filter.AllVisible"],
            Color: null,
            IsAllVisible: true);
        LayerFilterOptions.Add(allVisible);

        foreach (LayerListItem layer in Layers)
        {
            LayerFilterOptions.Add(new LayerFilterOption(
                layer.Id,
                layer.Name,
                layer.Color,
                IsAllVisible: false));
        }

        _selectedLayerFilter =
            LayerFilterOptions.FirstOrDefault(option => string.Equals(option.Id, selectedId, StringComparison.Ordinal)) ??
            allVisible;

        OnPropertyChanged(nameof(LayerFilterOptions));
        OnPropertyChanged(nameof(SelectedLayerFilter));
    }

    private static bool IsAnnotationVisible(
        string? effectiveLayerId,
        HashSet<string> visibleLayerIds) =>
        effectiveLayerId is null || visibleLayerIds.Contains(effectiveLayerId);

    private bool IsAnnotationInSelectedFilter(string? effectiveLayerId)
    {
        if (SelectedLayerFilter is null || SelectedLayerFilter.IsAllVisible)
        {
            return true;
        }

        return string.Equals(effectiveLayerId, SelectedLayerFilter.Id, StringComparison.Ordinal);
    }

    private BookmarkListItem? CurrentPageBookmark() =>
        Bookmarks.FirstOrDefault(bookmark => bookmark.PageIndex == CurrentPageIndex);

    private void SortBookmarks()
    {
        if (Bookmarks.Count <= 1)
        {
            return;
        }

        IEnumerable<BookmarkListItem> sorted = SelectedBookmarkSortOption?.Id == BookmarkSortByCreatedId
            ? Bookmarks
                .OrderBy(bookmark => bookmark.CreatedUtc)
                .ThenBy(bookmark => bookmark.PageIndex)
                .ThenBy(bookmark => bookmark.Id)
            : Bookmarks
                .OrderBy(bookmark => bookmark.PageIndex)
                .ThenBy(bookmark => bookmark.CreatedUtc)
                .ThenBy(bookmark => bookmark.Id);

        BookmarkListItem[] ordered = sorted.ToArray();
        if (ordered.SequenceEqual(Bookmarks))
        {
            return;
        }

        Bookmarks.Clear();
        foreach (BookmarkListItem bookmark in ordered)
        {
            Bookmarks.Add(bookmark);
        }
    }

    private void RefreshBookmarkSortOptions()
    {
        string selectedId = SelectedBookmarkSortOption?.Id ?? BookmarkSortByPageId;

        BookmarkSortOptions.Clear();
        BookmarkSortOptions.Add(new BookmarkSortOption(
            BookmarkSortByPageId,
            _localization["Bookmark.Sort.Page"]));
        BookmarkSortOptions.Add(new BookmarkSortOption(
            BookmarkSortByCreatedId,
            _localization["Bookmark.Sort.Created"]));

        SelectedBookmarkSortOption =
            BookmarkSortOptions.FirstOrDefault(option => option.Id == selectedId) ??
            BookmarkSortOptions[0];
    }

    private AnnotationRegion DefaultAnnotationRegion()
    {
        double top = Math.Min(0.82, 0.18 + (Annotations.Count * 0.075));
        return new AnnotationRegion(
            CurrentPageIndex,
            NormLeft: 0.18,
            NormTop: top,
            NormWidth: 0.42,
            NormHeight: 0.055);
    }

    private void RaiseLocalizedProperties()
    {
        OnPropertyChanged(nameof(PageStatusText));
        OnPropertyChanged(nameof(ReaderTitle));
        OnPropertyChanged(nameof(AnnotationPanelLabel));
        OnPropertyChanged(nameof(CreateHighlightLabel));
        OnPropertyChanged(nameof(ChooseHighlightColorLabel));
        OnPropertyChanged(nameof(CreateNoteLabel));
        OnPropertyChanged(nameof(DeleteAnnotationLabel));
        OnPropertyChanged(nameof(ConfirmDeleteAnnotationLabel));
        OnPropertyChanged(nameof(CancelDeleteAnnotationLabel));
        OnPropertyChanged(nameof(DeleteAnnotationConfirmationText));
        OnPropertyChanged(nameof(EditNoteLabel));
        OnPropertyChanged(nameof(NoteEditorLabel));
        OnPropertyChanged(nameof(BookmarkPanelLabel));
        OnPropertyChanged(nameof(BookmarkPanelAccessibleLabel));
        OnPropertyChanged(nameof(BookmarkSortLabel));
        RefreshBookmarkSortOptions();
        OnPropertyChanged(nameof(AddBookmarkLabel));
        OnPropertyChanged(nameof(RemoveBookmarkLabel));
        OnPropertyChanged(nameof(RenameBookmarkLabel));
        OnPropertyChanged(nameof(LayerPanelLabel));
        OnPropertyChanged(nameof(LayerFilterLabel));
        OnPropertyChanged(nameof(AddLayerLabel));
        OnPropertyChanged(nameof(MergeLayerLabel));
        OnPropertyChanged(nameof(LayerVisibilityLabel));
        OnPropertyChanged(nameof(DeleteLayerLabel));
        OnPropertyChanged(nameof(ReadingMemoryLabel));
        OnPropertyChanged(nameof(OpenedBecauseLabel));
        OnPropertyChanged(nameof(KeyInsightLabel));
        OnPropertyChanged(nameof(OpenQuestionsLabel));
        OnPropertyChanged(nameof(DispositionLabel));
        OnPropertyChanged(nameof(SaveReadingMemoryLabel));
        OnPropertyChanged(nameof(CaptureCitationLabel));
        OnPropertyChanged(nameof(CopyCitationLabel));
        OnPropertyChanged(nameof(ExportCitationLabel));
        OnPropertyChanged(nameof(CloseCitationLabel));
        RefreshLayerFilterOptions();
        RefreshHighlightColorOptions();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A display row for a bookmark in the reader sidebar.</summary>
public sealed class BookmarkListItem : INotifyPropertyChanged
{
    private string _label;

    /// <summary>Creates a new bookmark list item.</summary>
    public BookmarkListItem(
        long id,
        int pageIndex,
        string label,
        DateTimeOffset createdUtc,
        string accessibleLabelFormat)
    {
        Id = id;
        PageIndex = pageIndex;
        _label = label;
        PersistedLabel = label;
        CreatedUtc = createdUtc;
        AccessibleLabelFormat = accessibleLabelFormat;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The persisted bookmark identifier.</summary>
    public long Id { get; }

    /// <summary>The zero-based page index.</summary>
    public int PageIndex { get; }

    /// <summary>The UTC timestamp when the bookmark was created.</summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>The editable bookmark label.</summary>
    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleLabel)));
            }
        }
    }

    /// <summary>The last label confirmed by the bookmark service.</summary>
    public string PersistedLabel { get; private set; }

    /// <summary>Human-readable page number.</summary>
    public int PageNumber => PageIndex + 1;

    /// <summary>Localized accessible label including the bookmark name and page number.</summary>
    public string AccessibleLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        AccessibleLabelFormat,
        Label,
        PageNumber);

    private string AccessibleLabelFormat { get; }

    /// <summary>Marks the current editable label as persisted after a successful save.</summary>
    public void MarkPersistedLabel(string label) => PersistedLabel = label;
}

/// <summary>A bookmark panel sort option.</summary>
public sealed record BookmarkSortOption(string Id, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>A display row for an annotation layer in the reader sidebar.</summary>
public sealed class LayerListItem : INotifyPropertyChanged
{
    private string _name;
    private readonly string _activeFormat;
    private readonly string _visibleFormat;
    private readonly string _hiddenFormat;
    private readonly string _mergeFormat;
    private readonly string _deleteFormat;

    /// <summary>Creates a new layer list item.</summary>
    public LayerListItem(
        string id,
        string name,
        string color,
        bool isVisible,
        bool isActiveWritableLayer,
        bool canMergeOrDelete,
        string activeLabel,
        string activeFormat,
        string visibleFormat,
        string hiddenFormat,
        string mergeFormat,
        string deleteFormat)
    {
        Id = id;
        _name = name;
        PersistedName = name;
        Color = color;
        IsVisible = isVisible;
        IsActiveWritableLayer = isActiveWritableLayer;
        CanMergeOrDelete = canMergeOrDelete;
        ActiveLabel = activeLabel;
        _activeFormat = activeFormat;
        _visibleFormat = visibleFormat;
        _hiddenFormat = hiddenFormat;
        _mergeFormat = mergeFormat;
        _deleteFormat = deleteFormat;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The stable layer identifier.</summary>
    public string Id { get; }

    /// <summary>The last name confirmed by the layer service.</summary>
    public string PersistedName { get; private set; }

    /// <summary>The editable layer name.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveAutomationLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibilityAutomationLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MergeAutomationLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteAutomationLabel)));
            }
        }
    }

    /// <summary>The layer color.</summary>
    public string Color { get; }

    /// <summary>Whether the layer is currently visible.</summary>
    public bool IsVisible { get; }

    /// <summary>Whether this visible layer receives newly created highlights and notes.</summary>
    public bool IsActiveWritableLayer { get; }

    /// <summary>Localized active-layer marker text.</summary>
    public string ActiveLabel { get; }

    /// <summary>Whether destructive/combining layer actions are allowed.</summary>
    public bool CanMergeOrDelete { get; }

    /// <summary>Automation label for the active writable layer marker.</summary>
    public string ActiveAutomationLabel => FormatLayerLabel(_activeFormat);

    /// <summary>Automation label that includes the current visibility state.</summary>
    public string VisibilityAutomationLabel => FormatLayerLabel(IsVisible ? _visibleFormat : _hiddenFormat);

    /// <summary>Premium SVG icon path for the current visibility state.</summary>
    public string VisibilityIconPath => IconCatalog.GetAvaresPath(
        IsVisible ? "ic_layer_visible" : "ic_layer_hidden")!;

    /// <summary>Automation label for merging this layer.</summary>
    public string MergeAutomationLabel => FormatLayerLabel(_mergeFormat);

    /// <summary>Automation label for deleting this layer.</summary>
    public string DeleteAutomationLabel => FormatLayerLabel(_deleteFormat);

    private string FormatLayerLabel(string format) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, format, Name);

    /// <summary>Marks the current editable name as persisted after a successful save.</summary>
    public void MarkPersistedName(string name) => PersistedName = name;
}

/// <summary>A display option for filtering annotations by layer.</summary>
public sealed record LayerFilterOption(
    string Id,
    string Name,
    string? Color,
    bool IsAllVisible);

/// <summary>A display row for an annotation in the reader sidebar.</summary>
public sealed record AnnotationListItem(
    string Id,
    string Kind,
    string Preview,
    string? LayerId,
    string Color,
    bool IsNote,
    string? NoteText);

/// <summary>A selectable highlight color swatch.</summary>
public sealed record HighlightColorOption(
    string Id,
    string Color,
    string Label,
    string AccessibleLabel,
    bool IsLayerDefault,
    bool IsSelected)
{
    /// <summary>Small marker shown on the selected swatch.</summary>
    public string SelectedMarker => IsSelected ? "*" : string.Empty;
}

/// <summary>A captured citation card ready for display and plain-text export.</summary>
public sealed record CitationCardItem(
    string BookId,
    string Title,
    string Author,
    int PageNumber,
    string SelectedText,
    string PlainText,
    string PageText)
{
    /// <summary>Creates a display item from the domain citation card.</summary>
    public static CitationCardItem From(
        CitationCard card,
        string unknownTitle,
        string unknownAuthor,
        string pageFormat)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new CitationCardItem(
            card.BookId,
            card.Title ?? unknownTitle,
            card.Author ?? unknownAuthor,
            card.PageNumber,
            card.SelectedText,
            card.ToPlainText(unknownAuthor, unknownTitle, pageFormat),
            string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                pageFormat,
                card.PageNumber));
    }
}

/// <summary>A rendered overlay rectangle for an annotation on the active page.</summary>
public sealed record AnnotationOverlayItem(
    string AnnotationId,
    double X,
    double Y,
    double Width,
    double Height,
    string Color,
    string AccessibleLabel,
    string NoteAnchorAccessibleLabel,
    bool IsNote)
{
    /// <summary>Absolute position inside the page host.</summary>
    public Thickness Margin => new(X, Y, 0, 0);

    /// <summary>Absolute position of the note anchor marker.</summary>
    public Thickness NoteAnchorMargin => new(X + Math.Max(0, Width - 10), Math.Max(0, Y - 5), 0, 0);

    /// <summary>Contrast-safe display color for the translucent page overlay.</summary>
    public string OverlayColor => Color.ToUpperInvariant() switch
    {
        "#FFCC66" => "#A15C00",
        "#88AA77" => "#4A7A5A",
        "#C7795A" => "#A3452C",
        "#8E5A8A" => "#6A3A7A",
        _ => Color,
    };
}

/// <summary>A drag selection rectangle on the active page surface.</summary>
public sealed record SelectionOverlayItem(
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>Absolute position inside the page host.</summary>
    public Thickness Margin => new(X, Y, 0, 0);

    /// <summary>Position for the selection action menu.</summary>
    public Thickness ActionMargin => new(X, Math.Max(0, Y - 44), 0, 0);
}
