using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// View model for the sort and filter panel (FR-CAT-002).
/// Holds every filter dimension; <see cref="HasActiveFilters"/> indicates
/// whether any filter is active. <see cref="ClearAll"/> resets all dimensions.
/// <see cref="ToCriteria"/> converts the live state into the shared
/// <see cref="CatalogueFilter"/> criteria model (Sept-23 Phase 10).
/// </summary>
public sealed class CatalogueFilterViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<CatalogueSortField> _availableSortFields =
        Enum.GetValues<CatalogueSortField>();
    private readonly ILocalizationService? _localization;
    private int? _statusFilter;
    private int? _minRating;
    private int? _maxRating;
    private bool? _availabilityFilter;
    private string? _titleSearch;
    private string? _authorSearch;
    private bool _needsOcrOnly;
    private string? _selectedShelfId;
    private double? _qualityBelow;
    private CatalogueMissingFields _missingFields;
    private bool _possibleDuplicatesOnly;

    private CatalogueSortField _sortField = CatalogueSortField.Title;
    private bool _sortAscending = true;

    /// <summary>Initializes a new instance of <see cref="CatalogueFilterViewModel"/>.</summary>
    /// <param name="localization">Localises sort labels; English fallbacks are used when absent.</param>
    public CatalogueFilterViewModel(ILocalizationService? localization = null)
    {
        _localization = localization;
    }

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

    /// <summary>
    /// "Needs OCR" filter (Sept-23 Phase 17): only books that are image only, partly
    /// searchable, or whose OCR failed.
    /// </summary>
    public bool NeedsOcrOnly
    {
        get => _needsOcrOnly;
        set => SetField(ref _needsOcrOnly, value);
    }

    /// <summary>Only books whose metadata quality score is below this value in [0, 1].</summary>
    public double? QualityBelow
    {
        get => _qualityBelow;
        set => SetField(ref _qualityBelow, value is { } quality ? Math.Clamp(quality, 0, 1) : null);
    }

    /// <summary>Bibliographic fields that must all be missing.</summary>
    public CatalogueMissingFields MissingFields
    {
        get => _missingFields;
        set => SetField(ref _missingFields, value);
    }

    /// <summary>Only books with several copies or editions.</summary>
    public bool PossibleDuplicatesOnly
    {
        get => _possibleDuplicatesOnly;
        set => SetField(ref _possibleDuplicatesOnly, value);
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

    /// <summary>Accessible, localised text for the current sort direction.</summary>
    public string SortDirectionText => SortAscending
        ? Localize("Catalogue.Sort.Ascending", "Ascending")
        : Localize("Catalogue.Sort.Descending", "Descending");

    /// <summary>The localised menu label for a sort field.</summary>
    /// <param name="field">The sort field.</param>
    /// <returns>The label, for example "Sort by date added".</returns>
    public string SortFieldLabel(CatalogueSortField field) =>
        Localize("Catalogue.Sort." + field, field.ToString());

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
        SelectedShelfId is not null ||
        NeedsOcrOnly ||
        QualityBelow.HasValue ||
        MissingFields != CatalogueMissingFields.None ||
        PossibleDuplicatesOnly;

    /// <summary>The live state as the shared criteria model, with sort and no paging.</summary>
    /// <returns>The equivalent <see cref="CatalogueFilter"/>.</returns>
    public CatalogueFilter ToCriteria() => new(
        TitleContains: string.IsNullOrWhiteSpace(TitleSearch) ? null : TitleSearch.Trim(),
        AuthorContains: string.IsNullOrWhiteSpace(AuthorSearch) ? null : AuthorSearch.Trim(),
        ShelfId: SelectedShelfId,
        Status: StatusFilter,
        MinRating: MinRating,
        MaxRating: MaxRating,
        IsAvailable: AvailabilityFilter,
        QualityBelow: QualityBelow,
        MissingFields: MissingFields,
        NeedsOcr: NeedsOcrOnly,
        PossibleDuplicates: PossibleDuplicatesOnly,
        SortField: SortField,
        SortAscending: SortAscending);

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
        _needsOcrOnly = false;
        _qualityBelow = null;
        _missingFields = CatalogueMissingFields.None;
        _possibleDuplicatesOnly = false;
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(MinRating));
        OnPropertyChanged(nameof(MaxRating));
        OnPropertyChanged(nameof(AvailabilityFilter));
        OnPropertyChanged(nameof(TitleSearch));
        OnPropertyChanged(nameof(AuthorSearch));
        OnPropertyChanged(nameof(SelectedShelfId));
        OnPropertyChanged(nameof(NeedsOcrOnly));
        OnPropertyChanged(nameof(QualityBelow));
        OnPropertyChanged(nameof(MissingFields));
        OnPropertyChanged(nameof(PossibleDuplicatesOnly));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    /// <summary>Raises change notifications for culture-dependent text.</summary>
    public void RefreshLocalizedText() => OnPropertyChanged(nameof(SortDirectionText));

    private string Localize(string key, string fallback)
    {
        if (_localization is null)
        {
            return fallback;
        }

        string value = _localization[key];
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)
            ? fallback
            : value;
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
