using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Available sort fields for the catalogue (FR-CAT-002).
/// </summary>
public enum CatalogueSortField
{
    /// <summary>Sort by bibliographic title.</summary>
    Title,

    /// <summary>Sort by primary author.</summary>
    Author,

    /// <summary>Sort by publication year.</summary>
    Year,

    /// <summary>Sort by reader rating.</summary>
    Rating,

    /// <summary>Sort by lifecycle status.</summary>
    Status,
}

/// <summary>
/// View model for the sort and filter panel (FR-CAT-002).
/// Holds every filter dimension; <see cref="HasActiveFilters"/> indicates
/// whether any filter is active. <see cref="ClearAll"/> resets all dimensions.
/// </summary>
public sealed class CatalogueFilterViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<CatalogueSortField> _availableSortFields =
        Enum.GetValues<CatalogueSortField>();
    private int? _statusFilter;
    private int? _minRating;
    private int? _maxRating;
    private bool? _availabilityFilter;
    private string? _titleSearch;
    private string? _authorSearch;
    private string? _selectedShelfId;

    private CatalogueSortField _sortField = CatalogueSortField.Title;
    private bool _sortAscending = true;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Filter by lifecycle status (0=Active, 1=Unavailable, 2=Excluded).
    /// <see langword="null"/> means all statuses.
    /// </summary>
    public int? StatusFilter
    {
        get => _statusFilter;
        set => SetField(ref _statusFilter, value);
    }

    /// <summary>Minimum rating filter (1–5); <see langword="null"/> for no minimum.</summary>
    public int? MinRating
    {
        get => _minRating;
        set => SetField(ref _minRating, value);
    }

    /// <summary>Maximum rating filter (1–5); <see langword="null"/> for no maximum.</summary>
    public int? MaxRating
    {
        get => _maxRating;
        set => SetField(ref _maxRating, value);
    }

    /// <summary>
    /// Availability filter. <see langword="true"/> = available only,
    /// <see langword="false"/> = unavailable only,
    /// <see langword="null"/> = all.
    /// </summary>
    public bool? AvailabilityFilter
    {
        get => _availabilityFilter;
        set => SetField(ref _availabilityFilter, value);
    }

    /// <summary>Title substring search (case-insensitive); <see langword="null"/> for no filter.</summary>
    public string? TitleSearch
    {
        get => _titleSearch;
        set => SetField(ref _titleSearch, value);
    }

    /// <summary>Author substring search (case-insensitive); <see langword="null"/> for no filter.</summary>
    public string? AuthorSearch
    {
        get => _authorSearch;
        set => SetField(ref _authorSearch, value);
    }

    /// <summary>Selected shelf ID; <see langword="null"/> shows all shelves.</summary>
    public string? SelectedShelfId
    {
        get => _selectedShelfId;
        set => SetField(ref _selectedShelfId, value);
    }

    /// <summary>The field to sort by.</summary>
    public CatalogueSortField SortField
    {
        get => _sortField;
        set => SetField(ref _sortField, value);
    }

    /// <summary>True for ascending sort; false for descending.</summary>
    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (_sortAscending != value)
            {
                _sortAscending = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SortDirectionText));
            }
        }
    }

    /// <summary>The validated sort fields exposed by the catalogue filter UI.</summary>
    public IReadOnlyList<CatalogueSortField> SortOptions => _availableSortFields;

    /// <summary>Accessible text for the current sort direction.</summary>
    public string SortDirectionText => SortAscending ? "Ascending" : "Descending";

    /// <summary>Toggles the direction of the current stable sort.</summary>
    public void ToggleSortDirection()
    {
        SortAscending = !SortAscending;
    }

    /// <summary>True when at least one filter dimension is active.</summary>
    public bool HasActiveFilters =>
        StatusFilter.HasValue ||
        MinRating.HasValue ||
        MaxRating.HasValue ||
        AvailabilityFilter.HasValue ||
        !string.IsNullOrWhiteSpace(TitleSearch) ||
        !string.IsNullOrWhiteSpace(AuthorSearch) ||
        SelectedShelfId is not null;

    /// <summary>Resets every filter dimension and raises <see cref="PropertyChanged"/>.</summary>
    public void ClearAll()
    {
        _statusFilter = null;
        _minRating = null;
        _maxRating = null;
        _availabilityFilter = null;
        _titleSearch = null;
        _authorSearch = null;
        _selectedShelfId = null;
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(MinRating));
        OnPropertyChanged(nameof(MaxRating));
        OnPropertyChanged(nameof(AvailabilityFilter));
        OnPropertyChanged(nameof(TitleSearch));
        OnPropertyChanged(nameof(AuthorSearch));
        OnPropertyChanged(nameof(SelectedShelfId));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
