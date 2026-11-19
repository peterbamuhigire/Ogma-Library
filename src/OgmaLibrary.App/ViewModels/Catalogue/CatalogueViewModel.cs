using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The shared view model for all catalogue views (grid, list, directory).
/// It holds the in-memory list of all books, applies sort and filter in-memory via
/// LINQ (&lt; 150 ms P95 on 2,000 books per NFR-OGMA-003), and exposes the filtered
/// collection for virtualized binding. All views bind to <see cref="FilteredItems"/>;
/// selecting a book calls <see cref="OpenDetailAsync"/>.
/// </summary>
public sealed class CatalogueViewModel : INotifyPropertyChanged, IDisposable
{
    private const int PageSize = 100;
    private readonly ICatalogueReadModel _readModel;
    private readonly IBookDetailNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly ILibrarySettingsService? _settings;
    private readonly ICatalogueViewStateStore? _viewStateStore;
    private readonly string? _assetRootPath;
    private readonly IUiDispatcher _ui;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCts;
    private int _loadGeneration;

    private readonly List<BookSummaryProjection> _allItems = [];
    private readonly ObservableCollection<BookSummaryProjection> _filteredItems = [];
    private readonly ObservableCollection<BookSummaryProjection> _homeStripItems = [];

    private BookSummaryProjection? _selectedBook;
    private CatalogueView _currentView = CatalogueView.Grid;
    private bool _isLoading;
    private string? _libraryRootPath;
    private int _currentPage = 1;
    private int _totalFilteredCount;
    private Dictionary<string, IReadOnlyList<SmartShelfCondition>?> _smartShelves = new(StringComparer.Ordinal);
    private bool _showAll = true;
    private const CatalogueMissingFields AllMissingFields =
        CatalogueMissingFields.Author | CatalogueMissingFields.Isbn |
        CatalogueMissingFields.Cover | CatalogueMissingFields.Year;
    private bool _viewStateApplied;
    private CancellationTokenSource? _viewStateSaveCts;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="navigation">The book-detail navigation service.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="settings">Optional persisted settings used to resolve local assets.</param>
    /// <param name="assetRootPath">The configured sidecar root used for local visual assets.</param>
    /// <param name="viewStateStore">Optional store for persisted catalogue presentation state.</param>
    /// <param name="uiDispatcher">Marshals bound state onto the UI thread (inline when absent).</param>
    /// <param name="logger">Optional logger.</param>
    public CatalogueViewModel(
        ICatalogueReadModel readModel,
        IBookDetailNavigationService navigation,
        ILocalizationService localization,
        ILibrarySettingsService? settings = null,
        string? assetRootPath = null,
        ICatalogueViewStateStore? viewStateStore = null,
        IUiDispatcher? uiDispatcher = null,
        ILogger<CatalogueViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _readModel = readModel;
        _navigation = navigation;
        _localization = localization;
        _settings = settings;
        _assetRootPath = assetRootPath;
        _viewStateStore = viewStateStore;
        _ui = uiDispatcher ?? InlineUiDispatcher.Instance;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _localization.CultureChanged += OnCultureChanged;

        Filter = new CatalogueFilterViewModel(localization);
        Filter.PropertyChanged += (_, _) => ApplyFilterAndSort();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The filter and sort state.</summary>
    public CatalogueFilterViewModel Filter { get; }

    /// <summary>
    /// The localisation service, exposed so item templates can compose localised accessible names
    /// (Sept-23 Phase 10). Raised again on culture change so bound names refresh.
    /// </summary>
    public ILocalizationService Localization => _localization;

    /// <summary>
    /// Whether the whole filtered result is listed (virtualised) rather than paged. On by default
    /// and persisted with the view state (Sept-23 Phase 10).
    /// </summary>
    public bool ShowAll
    {
        get => _showAll;
        set
        {
            if (_showAll != value)
            {
                _showAll = value;
                OnPropertyChanged();
                ApplyFilterAndSort(resetPage: true);
                OnPropertyChanged(nameof(IsPaged));
            }
        }
    }

    /// <summary>Whether the pager is in use: "Show all" is off and there is more than one page.</summary>
    public bool IsPaged => !_showAll && TotalPages > 1;

    /// <summary>Whether the result is large enough for the "Show all" choice to matter.</summary>
    public bool CanChoosePaging => _totalFilteredCount > PageSize;

    /// <summary>Localised label for the "Show all" toggle.</summary>
    public string ShowAllText => _localization["Catalogue.Paging.ShowAll"];

    /// <summary>Localised "cover unavailable" badge text.</summary>
    public string CoverUnavailableBadgeText => _localization["Catalogue.Badge.CoverUnavailable"];

    /// <summary>The filtered, sorted list of book summaries for binding.</summary>
    public ObservableCollection<BookSummaryProjection> FilteredItems => _filteredItems;

    /// <summary>
    /// Every selected book (Sept-23 Phase 10, multi-select). Bound to the grid and list
    /// <c>SelectedItems</c>; <see cref="SelectedBook"/> stays the primary selection that drives the
    /// detail pane. The selection survives a refresh that replaces changed cards.
    /// </summary>
    public ObservableCollection<BookSummaryProjection> SelectedBooks { get; } = [];

    /// <summary>The currently selected book.</summary>
    public BookSummaryProjection? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (!ReferenceEquals(_selectedBook, value))
            {
                _selectedBook = value;
                OnPropertyChanged();
                if (value is not null)
                {
                    _ = OpenDetailAsync(value.BookId);
                }
            }
        }
    }

    /// <summary>The active view mode (grid / list / directory / 3D shelf).</summary>
    public CatalogueView CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView != value)
            {
                _currentView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(IsDirectoryView));
                ScheduleViewStateSave();
            }
        }
    }

    /// <summary>True when the grid view is active.</summary>
    public bool IsGridView => _currentView == CatalogueView.Grid;

    /// <summary>True when the list view is active.</summary>
    public bool IsListView => _currentView == CatalogueView.List;

    /// <summary>True when the directory view is active.</summary>
    public bool IsDirectoryView => _currentView == CatalogueView.Directory;

    /// <summary>True while books are being loaded from the read model.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The derived-asset store root used only for local cover image loading (the
    /// app-data folder; Sept-23 Phase 05). The name is kept for existing bindings.
    /// </summary>
    public string? LibraryRootPath
    {
        get => _libraryRootPath;
        private set
        {
            if (_libraryRootPath != value)
            {
                _libraryRootPath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The number of items currently shown after filtering.</summary>
    public int FilteredCount => _filteredItems.Count;

    /// <summary>The number of items matching the active filter across all pages.</summary>
    public int TotalFilteredCount => _totalFilteredCount;

    /// <summary>The one-based page currently shown in the catalogue.</summary>
    public int CurrentPage => _currentPage;

    /// <summary>The number of pages required for the current filter.</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalFilteredCount / (double)PageSize));

    /// <summary>Whether a previous catalogue page is available.</summary>
    public bool CanGoToPreviousPage => !_showAll && _currentPage > 1;

    /// <summary>Whether a next catalogue page is available.</summary>
    public bool CanGoToNextPage => !_showAll && _currentPage < TotalPages;

    /// <summary>Localized page summary for the catalogue footer.</summary>
    public string PageSummaryText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Catalogue.Paging.PageFormat"],
        _currentPage,
        TotalPages,
        _totalFilteredCount);

    /// <summary>Localized previous-page label.</summary>
    public string PreviousPageText => _localization["Catalogue.Paging.Previous"];

    /// <summary>Localized next-page label.</summary>
    public string NextPageText => _localization["Catalogue.Paging.Next"];

    /// <summary>
    /// Library home strip (Sept-23 Phase 09): books in progress (furthest first), or the first
    /// shelved books when nothing is in progress. At most <see cref="HomeStripSize"/> items.
    /// </summary>
    public ObservableCollection<BookSummaryProjection> HomeStripItems => _homeStripItems;

    /// <summary>The maximum number of books in the home strip.</summary>
    public const int HomeStripSize = 8;

    /// <summary>Whether the home header is shown (the library has books).</summary>
    public bool HasHomeStrip => _homeStripItems.Count > 0;

    /// <summary>Whether any book is in progress (the strip reads "Continue reading").</summary>
    public bool HasBooksInProgress => _allItems.Any(IsInProgress);

    /// <summary>The time-of-day greeting for the library home header.</summary>
    public string HomeGreeting => DateTime.Now.Hour switch
    {
        < 12 => _localization["Catalogue.Home.Greeting.Morning"],
        < 18 => _localization["Catalogue.Home.Greeting.Afternoon"],
        _ => _localization["Catalogue.Home.Greeting.Evening"],
    };

    /// <summary>The home strip heading ("Continue reading" or "On your shelves").</summary>
    public string HomeStripTitle => HasBooksInProgress
        ? _localization["Catalogue.Home.ContinueReading"]
        : _localization["Catalogue.Home.OnYourShelves"];

    /// <summary>The home header summary line ("17 books in your library").</summary>
    public string HomeSummaryText => string.Format(
        _localization.CurrentCulture,
        _localization["Catalogue.Home.CountFormat"],
        _allItems.Count);

    /// <summary>Localized catalogue badge labels shared by grid and list views.</summary>
    public string IndexedBadgeText => _localization["Catalogue.Badge.Indexed"];

    /// <summary>Localized indexing-progress badge label.</summary>
    public string IndexingBadgeText => _localization["Catalogue.Badge.Indexing"];

    /// <summary>Localized indexing-failure badge label.</summary>
    public string IndexFailedBadgeText => _localization["Catalogue.Badge.IndexFailed"];

    /// <summary>Localized embedding badge label.</summary>
    public string EmbeddedBadgeText => _localization["Catalogue.Badge.Embedded"];

    /// <summary>Localized embedding-progress badge label.</summary>
    public string EmbeddingBadgeText => _localization["Catalogue.Badge.Embedding"];

    /// <summary>Localized embedding-failure badge label.</summary>
    public string EmbeddingFailedBadgeText => _localization["Catalogue.Badge.EmbeddingFailed"];

    /// <summary>Localized OCR-derived badge label.</summary>
    public string OcrBadgeText => _localization["Catalogue.Badge.Ocr"];

    /// <summary>Localized unavailable badge label.</summary>
    public string UnavailableBadgeText => _localization["Catalogue.Badge.Unavailable"];

    /// <summary>Localized badge for a password-protected book (Sept-23 Phase 05).</summary>
    public string LockedBadgeText => _localization["Catalogue.Badge.Locked"];

    /// <summary>Localized "Searchable" text-status badge (Sept-23 Phase 17).</summary>
    public string TextSearchableBadgeText => _localization["Catalogue.Badge.TextSearchable"];

    /// <summary>Localized "Partly searchable" text-status badge.</summary>
    public string TextPartlyBadgeText => _localization["Catalogue.Badge.TextPartly"];

    /// <summary>Localized "Scanned, needs OCR" text-status badge.</summary>
    public string TextImageOnlyBadgeText => _localization["Catalogue.Badge.TextImageOnly"];

    /// <summary>Localized "OCR in progress" text-status badge.</summary>
    public string TextOcrInProgressBadgeText => _localization["Catalogue.Badge.TextOcrInProgress"];

    /// <summary>Localized "OCR text" text-status badge; the confidence is shown beside it.</summary>
    public string TextOcrBadgeText => _localization["Catalogue.Badge.TextOcr"];

    /// <summary>Localized "No readable text" text-status badge.</summary>
    public string TextNoneBadgeText => _localization["Catalogue.Badge.TextNone"];

    /// <summary>Localized "OCR failed" text-status badge.</summary>
    public string TextOcrFailedBadgeText => _localization["Catalogue.Badge.TextOcrFailed"];

    /// <summary>Localized favourite badge label.</summary>
    public string FavouriteBadgeText => _localization["Catalogue.Badge.Favourite"];

    /// <summary>Localized quality-score format.</summary>
    public string QualityBadgeFormat => _localization["Catalogue.Badge.QualityFormat"];

    /// <summary>Localized fallback for a missing directory-view file path.</summary>
    public string DirectoryPathUnavailableText =>
        _localization["Catalogue.Directory.PathUnavailable"];

    /// <summary>Localized fallback for a missing directory-view title.</summary>
    public string DirectoryUntitledText => _localization["Search.Result.Untitled"];

    /// <summary>Moves to the previous bounded page when one exists.</summary>
    public void GoToPreviousPage()
    {
        if (!CanGoToPreviousPage)
        {
            return;
        }

        _currentPage--;
        ApplyFilterAndSort(resetPage: false);
        ScheduleViewStateSave();
    }

    /// <summary>Moves to the next bounded page when one exists.</summary>
    public void GoToNextPage()
    {
        if (!CanGoToNextPage)
        {
            return;
        }

        _currentPage++;
        ApplyFilterAndSort(resetPage: false);
        ScheduleViewStateSave();
    }

    /// <summary>The total number of books (before any filter).</summary>
    public int TotalCount => _allItems.Count;

    /// <summary>True when the catalogue has no books at all.</summary>
    public bool IsEmpty => _allItems.Count == 0 && !_isLoading;

    /// <summary>True when the library has books but the active filter hides all of them.</summary>
    public bool IsFilteredEmpty => _allItems.Count > 0 && _totalFilteredCount == 0 && !_isLoading;

    /// <summary>
    /// Loads all books from the read model into the in-memory collection,
    /// then applies the current filter and sort.
    /// </summary>
    /// <remarks>
    /// Sept-23 Phase 02 (T02.5, K72): refreshes are single-flight. A newer call cancels the
    /// running one and queued older calls are coalesced away; the list is built off the UI
    /// thread and swapped in, with every bound mutation marshalled through
    /// <see cref="IUiDispatcher"/>. A superseded call returns without publishing.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = Interlocked.Increment(ref _loadGeneration);
        var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Interlocked.Exchange(ref _loadCts, loadCts)?.Cancel();
        CancellationToken token = loadCts.Token;

        try
        {
            await _loadGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AppLog.CatalogueLoadSuperseded(_logger);
            loadCts.Dispose();
            return;
        }

        try
        {
            if (generation != Volatile.Read(ref _loadGeneration))
            {
                // A newer refresh is queued behind this one; let it do the work.
                AppLog.CatalogueLoadSuperseded(_logger);
                return;
            }

            await _ui.InvokeAsync(() => IsLoading = true, token).ConfigureAwait(false);

            // Sept-23 Phase 10 (task 10.4): the persisted state is restored once per view-model
            // lifetime, so a refresh never overwrites a live filter change.
            if (!_viewStateApplied && _viewStateStore is not null)
            {
                CatalogueViewState? viewState = await _viewStateStore.LoadAsync(token).ConfigureAwait(false);
                _viewStateApplied = true;
                if (viewState is not null)
                {
                    await _ui.InvokeAsync(() => ApplyViewState(viewState), token).ConfigureAwait(false);
                }
            }

            // Sept-23 Phase 05 (K20): covers live in the app-data asset store, whatever
            // library folder the book came from; the chosen folder is never the asset root.
            string? libraryRoot = _assetRootPath ?? LibraryRootPath;

            // Build the new list off the UI thread, then swap it in on the UI thread.
            var items = new List<BookSummaryProjection>();
            var filter = new CatalogueFilter(MaxResults: 0);
            await foreach (var book in _readModel.GetBookSummariesAsync(filter, token)
                               .ConfigureAwait(false))
            {
                items.Add(book);
            }

            token.ThrowIfCancellationRequested();
            await _ui.InvokeAsync(
                () =>
                {
                    LibraryRootPath = libraryRoot;
                    _allItems.Clear();
                    _allItems.AddRange(items);
                    ApplyFilterAndSort(resetPage: false);
                },
                token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AppLog.CatalogueLoadSuperseded(_logger);
        }
        finally
        {
            _loadGate.Release();
            if (generation == Volatile.Read(ref _loadGeneration))
            {
                _ui.Post(() =>
                {
                    IsLoading = false;
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(TotalCount));
                });
            }

            Interlocked.CompareExchange(ref _loadCts, null, loadCts);
            loadCts.Dispose();
        }
    }

    /// <summary>
    /// Opens the book-detail panel for the specified book via the navigation service.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        await _navigation.OpenDetailAsync(bookId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the current filter and sort to <see cref="_allItems"/> and replaces
    /// the content of <see cref="FilteredItems"/>. Runs synchronously on the caller's
    /// thread; for 2,000 items this completes well within 150 ms (NFR-OGMA-003).
    /// </summary>
    public void ApplyFilterAndSort() => ApplyFilterAndSort(resetPage: true);

    /// <summary>
    /// Supplies the smart shelves' saved conditions, so selecting a smart shelf filters the
    /// in-memory list exactly as the read model counts it. A damaged query shows no books.
    /// Call on the UI thread.
    /// </summary>
    /// <param name="shelves">The current shelves; manual shelves are ignored.</param>
    public void SetSmartShelves(IEnumerable<ShelfProjection> shelves)
    {
        ArgumentNullException.ThrowIfNull(shelves);
        var next = new Dictionary<string, IReadOnlyList<SmartShelfCondition>?>(StringComparer.Ordinal);
        foreach (ShelfProjection shelf in shelves.Where(shelf => shelf.IsSmart))
        {
            next[shelf.ShelfId] = SmartShelfQueryParser.TryParse(shelf.Query, out var conditions) ? conditions : null;
        }

        bool affectsCurrent = Filter.SelectedShelfId is { } selected &&
            (_smartShelves.ContainsKey(selected) || next.ContainsKey(selected));
        _smartShelves = next;
        if (affectsCurrent)
        {
            ApplyFilterAndSort(resetPage: false);
        }
    }

    private void ApplyFilterAndSort(bool resetPage)
    {
        if (resetPage)
        {
            _currentPage = 1;
        }

        // Sept-23 Phase 10: one criteria model. The read model is authoritative; this is its
        // in-memory twin, bound to it by Sept23Phase10CriteriaTests. A smart shelf has no
        // membership rows, so (like the read model) its saved conditions select the books.
        CatalogueFilter criteria = Filter.ToCriteria();
        IEnumerable<BookSummaryProjection> source = _allItems;
        if (criteria.ShelfId is { } shelfId && _smartShelves.TryGetValue(shelfId, out var conditions))
        {
            source = conditions is null ? [] : SmartShelfEvaluator.Evaluate(_allItems, conditions);
            criteria = criteria with { ShelfId = null };
        }

        var results = CatalogueCriteriaEvaluator.Apply(source, criteria).ToList();
        _totalFilteredCount = results.Count;
        int totalPages = TotalPages;
        if (_currentPage > totalPages)
        {
            _currentPage = totalPages;
        }

        if (!_showAll)
        {
            results = results
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        List<BookSummaryProjection> strip = BuildHomeStrip();

        void UpdateFiltered()
        {
            // Sept-23 Phase 09: update in place so unchanged cards keep their containers (no
            // flicker, stable UI Automation elements) while background processing refreshes.
            string[] selectedIds = SelectedBooks.Select(book => book.BookId).ToArray();
            SyncInPlace(_filteredItems, results);
            SyncInPlace(_homeStripItems, strip);
            RestoreSelection(selectedIds);

            OnPropertyChanged(nameof(HasHomeStrip));
            OnPropertyChanged(nameof(HasBooksInProgress));
            OnPropertyChanged(nameof(HomeStripTitle));
            OnPropertyChanged(nameof(HomeSummaryText));
            OnPropertyChanged(nameof(HomeGreeting));

            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalFilteredCount));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
            OnPropertyChanged(nameof(PageSummaryText));
            OnPropertyChanged(nameof(IsPaged));
            OnPropertyChanged(nameof(CanChoosePaging));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsFilteredEmpty));
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateFiltered();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateFiltered);
        }

        if (resetPage)
        {
            ScheduleViewStateSave();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _loadCts, null)?.Cancel();
        _viewStateSaveCts?.Cancel();
        _viewStateSaveCts?.Dispose();
        _localization.CultureChanged -= OnCultureChanged;
        // No unmanaged resources; Filter subscription uses a lambda so no explicit removal needed.
    }

    private void ApplyViewState(CatalogueViewState state)
    {
        if (Enum.TryParse(state.View, ignoreCase: true, out CatalogueView view))
        {
            _currentView = view;
            OnPropertyChanged(nameof(CurrentView));
            OnPropertyChanged(nameof(IsGridView));
            OnPropertyChanged(nameof(IsListView));
            OnPropertyChanged(nameof(IsDirectoryView));
        }

        Filter.TitleSearch = state.TitleSearch;
        Filter.AuthorSearch = state.AuthorSearch;
        Filter.StatusFilter = state.StatusFilter;
        Filter.MinRating = state.MinRating;
        Filter.MaxRating = state.MaxRating;
        Filter.AvailabilityFilter = state.AvailabilityFilter;
        Filter.SelectedShelfId = state.SelectedShelfId;
        Filter.QualityBelow = state.QualityBelow is { } quality && double.IsFinite(quality) ? quality : null;
        Filter.MissingFields =
            Enum.TryParse(state.MissingFields, ignoreCase: true, out CatalogueMissingFields missing) &&
            (missing & ~AllMissingFields) == 0
                ? missing
                : CatalogueMissingFields.None;
        Filter.NeedsOcrOnly = state.NeedsOcrOnly;
        Filter.PossibleDuplicatesOnly = state.PossibleDuplicatesOnly;
        if (Enum.TryParse(state.SortField, ignoreCase: true, out CatalogueSortField sortField) &&
            Enum.IsDefined(sortField))
        {
            Filter.SortField = sortField;
        }

        Filter.SortAscending = state.SortAscending;
        _currentPage = Math.Max(1, state.CurrentPage);
        if (_showAll != state.ShowAll)
        {
            _showAll = state.ShowAll;
            OnPropertyChanged(nameof(ShowAll));
        }
    }

    private void ScheduleViewStateSave()
    {
        if (_viewStateStore is null)
        {
            return;
        }

        _viewStateSaveCts?.Cancel();
        _viewStateSaveCts?.Dispose();
        _viewStateSaveCts = new CancellationTokenSource();
        CancellationToken cancellationToken = _viewStateSaveCts.Token;
        _ = SaveViewStateAfterDelayAsync(cancellationToken);
    }

    private async Task SaveViewStateAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            await _viewStateStore!.SaveAsync(
                new CatalogueViewState(
                    CurrentView.ToString(),
                    Filter.TitleSearch,
                    Filter.AuthorSearch,
                    Filter.StatusFilter,
                    Filter.MinRating,
                    Filter.MaxRating,
                    Filter.AvailabilityFilter,
                    Filter.SelectedShelfId,
                    Filter.SortField.ToString(),
                    Filter.SortAscending,
                    _currentPage,
                    _showAll,
                    Filter.QualityBelow,
                    Filter.MissingFields == CatalogueMissingFields.None ? null : Filter.MissingFields.ToString(),
                    Filter.NeedsOcrOnly,
                    Filter.PossibleDuplicatesOnly),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A newer view change superseded this save.
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// A card replaced by <see cref="SyncInPlace"/> drops out of the list control's selection.
    /// Re-select the visible cards by id so a membership change keeps the user's selection.
    /// </summary>
    private void RestoreSelection(string[] selectedIds)
    {
        if (selectedIds.Length == 0)
        {
            return;
        }

        List<BookSummaryProjection> wanted = selectedIds
            .Select(id => _filteredItems.FirstOrDefault(book => book.BookId == id))
            .OfType<BookSummaryProjection>()
            .ToList();

        bool unchanged = wanted.Count == SelectedBooks.Count &&
            wanted.Zip(SelectedBooks).All(pair => ReferenceEquals(pair.First, pair.Second));
        if (unchanged)
        {
            return;
        }

        for (int i = SelectedBooks.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(SelectedBooks[i]))
            {
                SelectedBooks.RemoveAt(i);
            }
        }

        foreach (BookSummaryProjection book in wanted)
        {
            if (!SelectedBooks.Contains(book))
            {
                SelectedBooks.Add(book);
            }
        }
    }

    private static void SyncInPlace(ObservableCollection<BookSummaryProjection> target, List<BookSummaryProjection> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (i >= target.Count)
            {
                target.Add(source[i]);
            }
            else if (!IsSameCard(target[i], source[i]))
            {
                target[i] = source[i];
            }
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static bool IsSameCard(BookSummaryProjection a, BookSummaryProjection b) =>
        ReferenceEquals(a, b) ||
        (a.BookId == b.BookId &&
         a.Title == b.Title &&
         a.CoverRelativePath == b.CoverRelativePath &&
         a.Status == b.Status &&
         a.Rating == b.Rating &&
         a.ReadingProgressPct == b.ReadingProgressPct &&
         a.IsAvailable == b.IsAvailable &&
         a.Year == b.Year &&
         a.IsFavourite == b.IsFavourite &&
         a.IsLocked == b.IsLocked &&
         a.CoverSource == b.CoverSource &&
         a.HasFailedCover == b.HasFailedCover &&
         a.CopyCount == b.CopyCount &&
         a.RelativePath == b.RelativePath &&
         Equals(a.Processing, b.Processing) &&
         a.Authors.SequenceEqual(b.Authors) &&
         a.ShelfIds.SequenceEqual(b.ShelfIds));

    private static bool IsInProgress(BookSummaryProjection book) =>
        book.ReadingProgressPct is > 0 and < 100;

    private List<BookSummaryProjection> BuildHomeStrip()
    {
        List<BookSummaryProjection> inProgress = _allItems
            .Where(IsInProgress)
            .OrderByDescending(b => b.ReadingProgressPct)
            .ThenBy(b => b.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Take(HomeStripSize)
            .ToList();
        return inProgress.Count > 0
            ? inProgress
            : _allItems
                .OrderBy(b => b.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Take(HomeStripSize)
                .ToList();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Filter.RefreshLocalizedText();
        OnPropertyChanged(nameof(HomeGreeting));
        OnPropertyChanged(nameof(HomeStripTitle));
        OnPropertyChanged(nameof(HomeSummaryText));
        OnPropertyChanged(nameof(PageSummaryText));
        OnPropertyChanged(nameof(PreviousPageText));
        OnPropertyChanged(nameof(NextPageText));
        OnPropertyChanged(nameof(IndexedBadgeText));
        OnPropertyChanged(nameof(IndexingBadgeText));
        OnPropertyChanged(nameof(IndexFailedBadgeText));
        OnPropertyChanged(nameof(EmbeddedBadgeText));
        OnPropertyChanged(nameof(EmbeddingBadgeText));
        OnPropertyChanged(nameof(EmbeddingFailedBadgeText));
        OnPropertyChanged(nameof(OcrBadgeText));
        OnPropertyChanged(nameof(UnavailableBadgeText));
        OnPropertyChanged(nameof(LockedBadgeText));
        OnPropertyChanged(nameof(TextSearchableBadgeText));
        OnPropertyChanged(nameof(TextPartlyBadgeText));
        OnPropertyChanged(nameof(TextImageOnlyBadgeText));
        OnPropertyChanged(nameof(TextOcrInProgressBadgeText));
        OnPropertyChanged(nameof(TextOcrBadgeText));
        OnPropertyChanged(nameof(TextNoneBadgeText));
        OnPropertyChanged(nameof(TextOcrFailedBadgeText));
        OnPropertyChanged(nameof(FavouriteBadgeText));
        OnPropertyChanged(nameof(QualityBadgeFormat));
        OnPropertyChanged(nameof(DirectoryPathUnavailableText));
        OnPropertyChanged(nameof(DirectoryUntitledText));
        OnPropertyChanged(nameof(ShowAllText));
        OnPropertyChanged(nameof(CoverUnavailableBadgeText));
        OnPropertyChanged(nameof(Localization));
    }
}

/// <summary>The available catalogue view modes (FR-CAT-001).</summary>
public enum CatalogueView
{
    /// <summary>Cover grid view.</summary>
    Grid,

    /// <summary>Table-style list view.</summary>
    List,

    /// <summary>File-system directory tree view.</summary>
    Directory,

    /// <summary>3D bookshelf view.</summary>
    Shelf3D,
}
