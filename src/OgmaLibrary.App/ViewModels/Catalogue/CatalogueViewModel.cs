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

    private BookSummaryProjection? _selectedBook;
    private CatalogueView _currentView = CatalogueView.Grid;
    private bool _isLoading;
    private string? _libraryRootPath;
    private int _currentPage = 1;
    private int _totalFilteredCount;
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

        Filter = new CatalogueFilterViewModel();
        Filter.PropertyChanged += (_, _) => ApplyFilterAndSort();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The filter and sort state.</summary>
    public CatalogueFilterViewModel Filter { get; }

    /// <summary>The filtered, sorted list of book summaries for binding.</summary>
    public ObservableCollection<BookSummaryProjection> FilteredItems => _filteredItems;

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
    public bool CanGoToPreviousPage => _currentPage > 1;

    /// <summary>Whether a next catalogue page is available.</summary>
    public bool CanGoToNextPage => _currentPage < TotalPages;

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

            CatalogueViewState? viewState = _viewStateStore is null
                ? null
                : await _viewStateStore.LoadAsync(token).ConfigureAwait(false);
            if (viewState is not null)
            {
                await _ui.InvokeAsync(() => ApplyViewState(viewState), token).ConfigureAwait(false);
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

    private void ApplyFilterAndSort(bool resetPage)
    {
        if (resetPage)
        {
            _currentPage = 1;
        }

        var query = _allItems.AsEnumerable();

        // Title substring filter.
        if (!string.IsNullOrWhiteSpace(Filter.TitleSearch))
        {
            string term = Filter.TitleSearch.Trim();
            query = query.Where(b =>
                b.Title != null &&
                b.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Author substring filter.
        if (!string.IsNullOrWhiteSpace(Filter.AuthorSearch))
        {
            string term = Filter.AuthorSearch.Trim();
            query = query.Where(b =>
                b.Authors.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // Status filter.
        if (Filter.StatusFilter.HasValue)
        {
            int status = Filter.StatusFilter.Value;
            query = query.Where(b => b.Status == status);
        }

        // Sept-23 Phase 17: "Needs OCR" (image only, partly searchable, or OCR failed).
        if (Filter.NeedsOcrOnly)
        {
            query = query.Where(b => b.Processing?.NeedsOcr == true && !b.IsLocked);
        }

        // Rating range filter.
        if (Filter.MinRating.HasValue)
        {
            int min = Filter.MinRating.Value;
            query = query.Where(b => b.Rating.HasValue && b.Rating.Value >= min);
        }

        if (Filter.MaxRating.HasValue)
        {
            int max = Filter.MaxRating.Value;
            query = query.Where(b => b.Rating.HasValue && b.Rating.Value <= max);
        }

        // Availability filter.
        if (Filter.AvailabilityFilter.HasValue)
        {
            bool avail = Filter.AvailabilityFilter.Value;
            query = query.Where(b => b.IsAvailable == avail);
        }

        // Shelf filter.
        if (Filter.SelectedShelfId is not null)
        {
            string shelfId = Filter.SelectedShelfId;
            query = query.Where(b => b.ShelfIds.Contains(shelfId));
        }

        // Sort.
        query = (Filter.SortField, Filter.SortAscending) switch
        {
            (CatalogueSortField.Title, true) => query.OrderBy(b => b.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (CatalogueSortField.Title, false) => query.OrderByDescending(b => b.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (CatalogueSortField.Author, true) => query.OrderBy(b => b.Authors.Count > 0 ? b.Authors[0] : string.Empty, StringComparer.OrdinalIgnoreCase),
            (CatalogueSortField.Author, false) => query.OrderByDescending(b => b.Authors.Count > 0 ? b.Authors[0] : string.Empty, StringComparer.OrdinalIgnoreCase),
            (CatalogueSortField.Year, true) => query.OrderBy(b => b.Year ?? 0),
            (CatalogueSortField.Year, false) => query.OrderByDescending(b => b.Year ?? 0),
            (CatalogueSortField.Rating, true) => query.OrderBy(b => b.Rating ?? 0),
            (CatalogueSortField.Rating, false) => query.OrderByDescending(b => b.Rating ?? 0),
            (CatalogueSortField.Status, true) => query.OrderBy(b => b.Status),
            (CatalogueSortField.Status, false) => query.OrderByDescending(b => b.Status),
            _ => query.OrderBy(b => b.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase),
        };

        var results = query.ToList();
        _totalFilteredCount = results.Count;
        int totalPages = TotalPages;
        if (_currentPage > totalPages)
        {
            _currentPage = totalPages;
        }

        results = results
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        void UpdateFiltered()
        {
            _filteredItems.Clear();
            foreach (var item in results)
            {
                _filteredItems.Add(item);
            }

            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalFilteredCount));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
            OnPropertyChanged(nameof(PageSummaryText));
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
        if (Enum.TryParse(state.SortField, ignoreCase: true, out CatalogueSortField sortField))
        {
            Filter.SortField = sortField;
        }

        Filter.SortAscending = state.SortAscending;
        _currentPage = Math.Max(1, state.CurrentPage);
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
                    _currentPage),
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

    private void OnCultureChanged(object? sender, EventArgs e)
    {
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
