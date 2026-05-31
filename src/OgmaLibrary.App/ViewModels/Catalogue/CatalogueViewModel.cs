using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
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
    private readonly ICatalogueReadModel _readModel;
    private readonly IBookDetailNavigationService _navigation;
    private readonly ILocalizationService _localization;

    private readonly List<BookSummaryProjection> _allItems = [];
    private readonly ObservableCollection<BookSummaryProjection> _filteredItems = [];

    private BookSummaryProjection? _selectedBook;
    private CatalogueView _currentView = CatalogueView.Grid;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="navigation">The book-detail navigation service.</param>
    /// <param name="localization">The localization service.</param>
    public CatalogueViewModel(
        ICatalogueReadModel readModel,
        IBookDetailNavigationService navigation,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _readModel = readModel;
        _navigation = navigation;
        _localization = localization;

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

    /// <summary>The active view mode (grid / list / directory / 3D placeholder).</summary>
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

    /// <summary>The number of items currently shown after filtering.</summary>
    public int FilteredCount => _filteredItems.Count;

    /// <summary>The total number of books (before any filter).</summary>
    public int TotalCount => _allItems.Count;

    /// <summary>True when the catalogue has no books at all.</summary>
    public bool IsEmpty => _allItems.Count == 0 && !_isLoading;

    /// <summary>
    /// Loads all books from the read model into the in-memory collection,
    /// then applies the current filter and sort.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            _allItems.Clear();

            var filter = new CatalogueFilter(MaxResults: 0);
            await foreach (var book in _readModel.GetBookSummariesAsync(filter, cancellationToken)
                               .ConfigureAwait(false))
            {
                _allItems.Add(book);
            }

            ApplyFilterAndSort();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(TotalCount));
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
    public void ApplyFilterAndSort()
    {
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

        void UpdateFiltered()
        {
            _filteredItems.Clear();
            foreach (var item in results)
            {
                _filteredItems.Add(item);
            }

            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(IsEmpty));
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateFiltered();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateFiltered);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources; Filter subscription uses a lambda so no explicit removal needed.
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

    /// <summary>3D bookshelf view (Phase 14 placeholder).</summary>
    Shelf3D,
}
