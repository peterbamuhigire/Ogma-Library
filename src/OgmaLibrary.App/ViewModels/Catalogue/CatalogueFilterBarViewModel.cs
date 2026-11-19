using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>One choice in a filter-bar list: the criterion value and its localised label.</summary>
/// <typeparam name="T">The criterion value type.</typeparam>
/// <param name="Value">The value; <see langword="null"/> means "any".</param>
/// <param name="Label">The localised label, also the item's accessible name.</param>
public sealed record FilterOption<T>(T Value, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// The catalogue filter bar (Sept-23 Phase 10, task 10.5; CAT-002, CAT-006, META-007): choice lists
/// for every criterion of the shared <see cref="CatalogueFilterViewModel"/>, one removable chip per
/// active criterion, the explained no-results text, and "Save as smart shelf". It holds no criteria
/// of its own; the catalogue filter is the single source of truth.
/// </summary>
public sealed class CatalogueFilterBarViewModel : INotifyPropertyChanged
{
    private static readonly double[] QualityThresholds = [0.5, 0.8];

    private static readonly (CatalogueMissingFields Field, string Id, string Key)[] MissingFieldChips =
    [
        (CatalogueMissingFields.Author, "missing.author", "Filter.Bar.Missing.Author"),
        (CatalogueMissingFields.Isbn, "missing.isbn", "Filter.Bar.Missing.Isbn"),
        (CatalogueMissingFields.Cover, "missing.cover", "Filter.Bar.Missing.Cover"),
        (CatalogueMissingFields.Year, "missing.year", "Filter.Bar.Missing.Year"),
    ];

    private readonly CatalogueFilterViewModel _filter;
    private readonly ShelfSidebarViewModel _shelves;
    private readonly ILocalizationService _localization;
    private readonly ILogger _logger;
    private string _smartShelfName = string.Empty;
    private string? _smartShelfStatus;
    private bool _isSaving;

    /// <summary>Initializes a new instance of <see cref="CatalogueFilterBarViewModel"/>.</summary>
    /// <param name="filter">The catalogue's live filter.</param>
    /// <param name="shelves">The collections, for the shelf choice and smart-shelf creation.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="logger">Logs a failed save (identifiers only).</param>
    public CatalogueFilterBarViewModel(
        CatalogueFilterViewModel filter,
        ShelfSidebarViewModel shelves,
        ILocalizationService localization,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(shelves);
        ArgumentNullException.ThrowIfNull(localization);
        _filter = filter;
        _shelves = shelves;
        _localization = localization;
        _logger = logger ?? NullLogger.Instance;
        BuildOptions();
        BuildShelfOptions();
        _filter.PropertyChanged += OnFilterChanged;
        _shelves.PropertyChanged += OnShelvesChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Labels ───────────────────────────────────────────────────────────────

    /// <summary>Status label.</summary>
    public string StatusLabel => _localization["Filter.Bar.Status"];

    /// <summary>Rating label.</summary>
    public string RatingLabel => _localization["Filter.Bar.Rating"];

    /// <summary>Availability label.</summary>
    public string AvailabilityLabel => _localization["Filter.Bar.Availability"];

    /// <summary>Metadata-quality label.</summary>
    public string QualityLabel => _localization["Filter.Bar.Quality"];

    /// <summary>Missing-details group label.</summary>
    public string MissingLabel => _localization["Filter.Bar.Missing"];

    /// <summary>"Author" missing-field label.</summary>
    public string MissingAuthorLabel => _localization["Filter.Bar.Missing.Author"];

    /// <summary>"ISBN" missing-field label.</summary>
    public string MissingIsbnLabel => _localization["Filter.Bar.Missing.Isbn"];

    /// <summary>"Cover" missing-field label.</summary>
    public string MissingCoverLabel => _localization["Filter.Bar.Missing.Cover"];

    /// <summary>"Year" missing-field label.</summary>
    public string MissingYearLabel => _localization["Filter.Bar.Missing.Year"];

    /// <summary>Accessible name for the missing-author check box ("Missing details: Author").</summary>
    public string MissingAuthorName => MissingName(MissingAuthorLabel);

    /// <summary>Accessible name for the missing-ISBN check box.</summary>
    public string MissingIsbnName => MissingName(MissingIsbnLabel);

    /// <summary>Accessible name for the missing-cover check box.</summary>
    public string MissingCoverName => MissingName(MissingCoverLabel);

    /// <summary>Accessible name for the missing-year check box.</summary>
    public string MissingYearName => MissingName(MissingYearLabel);

    /// <summary>"Needs OCR" label.</summary>
    public string NeedsOcrLabel => _localization["Filter.Bar.NeedsOcr"];

    /// <summary>"Possible duplicates" label.</summary>
    public string PossibleDuplicatesLabel => _localization["Filter.Bar.PossibleDuplicates"];

    /// <summary>Collection label.</summary>
    public string ShelfLabel => _localization["Filter.Bar.Shelf"];

    /// <summary>Smart-shelf name prompt.</summary>
    public string SmartShelfNameLabel => _localization["Filter.Bar.SmartShelfName"];

    /// <summary>"Save as smart shelf" action label.</summary>
    public string SaveSmartShelfLabel => _localization["Filter.Bar.SaveSmartShelf"];

    // ── Choice lists ─────────────────────────────────────────────────────────

    /// <summary>Lifecycle status choices.</summary>
    public IReadOnlyList<FilterOption<int?>> StatusOptions { get; private set; } = [];

    /// <summary>Minimum-rating choices.</summary>
    public IReadOnlyList<FilterOption<int?>> RatingOptions { get; private set; } = [];

    /// <summary>File availability choices.</summary>
    public IReadOnlyList<FilterOption<bool?>> AvailabilityOptions { get; private set; } = [];

    /// <summary>Metadata-quality threshold choices.</summary>
    public IReadOnlyList<FilterOption<double?>> QualityOptions { get; private set; } = [];

    /// <summary>Collection choices ("All collections" first).</summary>
    public IReadOnlyList<FilterOption<string?>> ShelfOptions { get; private set; } = [];

    /// <summary>The selected status choice.</summary>
    public FilterOption<int?> SelectedStatus
    {
        get => Pick(StatusOptions, _filter.StatusFilter);
        set => _filter.StatusFilter = value?.Value;
    }

    /// <summary>The selected minimum-rating choice.</summary>
    public FilterOption<int?> SelectedRating
    {
        get => Pick(RatingOptions, _filter.MinRating);
        set => _filter.MinRating = value?.Value;
    }

    /// <summary>The selected availability choice.</summary>
    public FilterOption<bool?> SelectedAvailability
    {
        get => Pick(AvailabilityOptions, _filter.AvailabilityFilter);
        set => _filter.AvailabilityFilter = value?.Value;
    }

    /// <summary>The selected quality choice.</summary>
    public FilterOption<double?> SelectedQuality
    {
        get => Pick(QualityOptions, _filter.QualityBelow);
        set => _filter.QualityBelow = value?.Value;
    }

    /// <summary>The selected collection choice.</summary>
    public FilterOption<string?> SelectedShelf
    {
        get => Pick(ShelfOptions, _filter.SelectedShelfId);
        set => _filter.SelectedShelfId = value?.Value;
    }

    /// <summary>Only books without an author.</summary>
    public bool MissingAuthor
    {
        get => _filter.MissingFields.HasFlag(CatalogueMissingFields.Author);
        set => SetMissing(CatalogueMissingFields.Author, value);
    }

    /// <summary>Only books without an ISBN.</summary>
    public bool MissingIsbn
    {
        get => _filter.MissingFields.HasFlag(CatalogueMissingFields.Isbn);
        set => SetMissing(CatalogueMissingFields.Isbn, value);
    }

    /// <summary>Only books without a cover.</summary>
    public bool MissingCover
    {
        get => _filter.MissingFields.HasFlag(CatalogueMissingFields.Cover);
        set => SetMissing(CatalogueMissingFields.Cover, value);
    }

    /// <summary>Only books without a publication year.</summary>
    public bool MissingYear
    {
        get => _filter.MissingFields.HasFlag(CatalogueMissingFields.Year);
        set => SetMissing(CatalogueMissingFields.Year, value);
    }

    // ── Chips and the no-results state ───────────────────────────────────────

    /// <summary>One removable chip per active criterion, in filter-bar order.</summary>
    public IReadOnlyList<FilterChip> Chips
    {
        get
        {
            var chips = new List<FilterChip>();
            CultureInfo culture = CultureInfo.CurrentCulture;
            void Add(string id, string label) => chips.Add(new FilterChip(
                id,
                label,
                string.Format(culture, _localization["Filter.Chip.RemoveFormat"], label)));
            string Format(string key, string value) => string.Format(culture, _localization[key], value);

            if (!string.IsNullOrWhiteSpace(_filter.TitleSearch))
            {
                Add("title", Format("Filter.Chip.TitleFormat", _filter.TitleSearch.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(_filter.AuthorSearch))
            {
                Add("author", Format("Filter.Chip.AuthorFormat", _filter.AuthorSearch.Trim()));
            }

            if (_filter.SelectedShelfId is { } shelfId)
            {
                string name = _shelves.Shelves.FirstOrDefault(shelf => shelf.ShelfId == shelfId)?.Name ?? shelfId;
                Add("shelf", Format("Filter.Chip.CollectionFormat", name));
            }

            if (_filter.StatusFilter.HasValue)
            {
                Add("status", Format("Filter.Chip.StatusFormat", SelectedStatus.Label));
            }

            if (_filter.MinRating.HasValue || _filter.MaxRating.HasValue)
            {
                Add("rating", Format("Filter.Chip.RatingFormat", RatingText(_filter.MinRating, _filter.MaxRating)));
            }

            if (_filter.AvailabilityFilter.HasValue)
            {
                Add("availability", Format("Filter.Chip.AvailabilityFormat", SelectedAvailability.Label));
            }

            if (_filter.QualityBelow is { } quality)
            {
                Add("quality", Format("Filter.Chip.QualityFormat", QualityText(quality)));
            }

            foreach ((CatalogueMissingFields missing, string id, string key) in MissingFieldChips)
            {
                if (_filter.MissingFields.HasFlag(missing))
                {
                    Add(id, Format("Filter.Chip.MissingFormat", _localization[key]));
                }
            }

            if (_filter.NeedsOcrOnly)
            {
                Add("ocr", NeedsOcrLabel);
            }

            if (_filter.PossibleDuplicatesOnly)
            {
                Add("duplicates", PossibleDuplicatesLabel);
            }

            return chips;
        }
    }

    /// <summary>The no-results explanation, naming every active filter.</summary>
    public string NoResultsText
    {
        get
        {
            IReadOnlyList<FilterChip> chips = Chips;
            return chips.Count == 0
                ? string.Empty
                : string.Format(
                    CultureInfo.CurrentCulture,
                    _localization["Catalogue.FilteredEmpty.ActiveFormat"],
                    string.Join(_localization["Filter.Chip.ListSeparator"], chips.Select(chip => chip.Label)));
        }
    }

    /// <summary>Removes the criterion behind one chip and leaves every other criterion unchanged.</summary>
    /// <param name="chipId">The chip identifier.</param>
    public void Remove(string chipId)
    {
        switch (chipId)
        {
            case "title":
                _filter.TitleSearch = null;
                break;
            case "author":
                _filter.AuthorSearch = null;
                break;
            case "shelf":
                _filter.SelectedShelfId = null;
                break;
            case "status":
                _filter.StatusFilter = null;
                break;
            case "rating":
                _filter.MinRating = null;
                _filter.MaxRating = null;
                break;
            case "availability":
                _filter.AvailabilityFilter = null;
                break;
            case "quality":
                _filter.QualityBelow = null;
                break;
            case "ocr":
                _filter.NeedsOcrOnly = false;
                break;
            case "duplicates":
                _filter.PossibleDuplicatesOnly = false;
                break;
            default:
                foreach ((CatalogueMissingFields field, string id, _) in MissingFieldChips)
                {
                    if (id == chipId)
                    {
                        SetMissing(field, false);
                    }
                }

                break;
        }
    }

    // ── Save as smart shelf ──────────────────────────────────────────────────

    /// <summary>The name typed for a new smart shelf.</summary>
    public string SmartShelfName
    {
        get => _smartShelfName;
        set
        {
            if (_smartShelfName != value)
            {
                _smartShelfName = value ?? string.Empty;
                OnPropertyChanged();
                RaiseSmartShelfState();
            }
        }
    }

    /// <summary>True when the live filter can be saved under the typed name.</summary>
    public bool CanSaveSmartShelf =>
        !_isSaving && !string.IsNullOrWhiteSpace(SmartShelfName) && SmartShelfHint.Length == 0;

    /// <summary>
    /// Why the live filter cannot be saved as a smart shelf (no filter, or a collection filter,
    /// which a smart shelf cannot contain); empty when it can.
    /// </summary>
    public string SmartShelfHint =>
        !_filter.HasActiveFilters ? _localization["Filter.Bar.SmartShelfNeedsFilter"]
        : _filter.SelectedShelfId is not null ? _localization["Filter.Bar.SmartShelfNeedsNoCollection"]
        : string.Empty;

    /// <summary>Whether <see cref="SmartShelfHint"/> is shown.</summary>
    public bool HasSmartShelfHint => SmartShelfHint.Length > 0;

    /// <summary>The result of the latest save, for a polite live region.</summary>
    public string? SmartShelfStatus
    {
        get => _smartShelfStatus;
        private set
        {
            if (_smartShelfStatus != value)
            {
                _smartShelfStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSmartShelfStatus));
            }
        }
    }

    /// <summary>Whether <see cref="SmartShelfStatus"/> is shown.</summary>
    public bool HasSmartShelfStatus => !string.IsNullOrWhiteSpace(SmartShelfStatus);

    /// <summary>Saves the live filter as a smart shelf named <see cref="SmartShelfName"/>.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SaveSmartShelfAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSaveSmartShelf)
        {
            SmartShelfStatus = HasSmartShelfHint ? SmartShelfHint : _localization["Catalogue.Shelves.NameRequired"];
            return;
        }

        string name = SmartShelfName.Trim();
        CatalogueFilter criteria = _filter.ToCriteria();
        _isSaving = true;
        RaiseSmartShelfState();
        try
        {
            // UI-thread continuation: this view model's bound state changes afterwards.
            await _shelves.CreateSmartShelfAsync(name, criteria, cancellationToken).ConfigureAwait(true);
            SmartShelfName = string.Empty;
            SmartShelfStatus = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Filter.Bar.SmartShelfSavedFormat"],
                name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
                                   !OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(CatalogueFilterBarViewModel), "catalogue.smart_shelf_save");
            SmartShelfStatus = _localization["Filter.Bar.SmartShelfFailed"];
        }
        finally
        {
            _isSaving = false;
            RaiseSmartShelfState();
        }
    }

    /// <summary>Rebuilds the localised labels and choices after a language change.</summary>
    public void RefreshLocalizedText()
    {
        BuildOptions();
        BuildShelfOptions();
        foreach (string name in new[]
                 {
                     nameof(StatusLabel), nameof(RatingLabel), nameof(AvailabilityLabel), nameof(QualityLabel),
                     nameof(MissingLabel), nameof(MissingAuthorLabel), nameof(MissingIsbnLabel),
                     nameof(MissingCoverLabel), nameof(MissingYearLabel), nameof(MissingAuthorName),
                     nameof(MissingIsbnName), nameof(MissingCoverName), nameof(MissingYearName),
                     nameof(NeedsOcrLabel), nameof(PossibleDuplicatesLabel), nameof(ShelfLabel),
                     nameof(SmartShelfNameLabel), nameof(SaveSmartShelfLabel), nameof(Chips), nameof(NoResultsText),
                     nameof(SmartShelfHint), nameof(HasSmartShelfHint),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private static FilterOption<T> Pick<T>(IReadOnlyList<FilterOption<T>> options, T value) =>
        options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value)) ?? options[0];

    private string MissingName(string field) =>
        string.Format(CultureInfo.CurrentCulture, "{0}: {1}", MissingLabel, field);

    private string RatingText(int? min, int? max)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        return (min, max) switch
        {
            ({ } low, { } high) => string.Format(culture, _localization["Filter.Bar.Rating.RangeFormat"], low, high),
            ({ } low, null) => string.Format(culture, _localization["Filter.Bar.Rating.AtLeastFormat"], low),
            (null, { } high) => string.Format(culture, _localization["Filter.Bar.Rating.AtMostFormat"], high),
            _ => _localization["Filter.Bar.Rating.Any"],
        };
    }

    private string QualityText(double threshold) =>
        string.Format(CultureInfo.CurrentCulture, _localization["Filter.Bar.Quality.BelowFormat"], threshold);

    private void BuildOptions()
    {
        StatusOptions =
        [
            new(null, _localization["Filter.Bar.Status.Any"]),
            new(0, _localization["Filter.Bar.Status.Active"]),
            new(1, _localization["Filter.Bar.Status.Unavailable"]),
            new(2, _localization["Filter.Bar.Status.Excluded"]),
        ];
        RatingOptions =
        [
            new FilterOption<int?>(null, _localization["Filter.Bar.Rating.Any"]),
            .. Enumerable.Range(1, 5).Select(stars => new FilterOption<int?>(stars, RatingText(stars, null))),
        ];
        AvailabilityOptions =
        [
            new(null, _localization["Filter.Bar.Availability.Any"]),
            new(true, _localization["Filter.Bar.Availability.Available"]),
            new(false, _localization["Filter.Bar.Availability.Missing"]),
        ];
        var quality = new List<FilterOption<double?>> { new(null, _localization["Filter.Bar.Quality.Any"]) };
        quality.AddRange(QualityThresholds.Select(threshold => new FilterOption<double?>(threshold, QualityText(threshold))));

        // A persisted threshold outside the offered list still shows (and stays selectable).
        if (_filter.QualityBelow is { } current && !QualityThresholds.Contains(current))
        {
            quality.Add(new FilterOption<double?>(current, QualityText(current)));
        }

        QualityOptions = quality;
        OnPropertyChanged(nameof(StatusOptions));
        OnPropertyChanged(nameof(RatingOptions));
        OnPropertyChanged(nameof(AvailabilityOptions));
        OnPropertyChanged(nameof(QualityOptions));
        RaiseSelections();
    }

    private void BuildShelfOptions()
    {
        var options = new List<FilterOption<string?>> { new(null, _localization["Filter.Bar.Shelf.Any"]) };
        options.AddRange(_shelves.Shelves.Select(shelf => new FilterOption<string?>(shelf.ShelfId, shelf.Name)));
        ShelfOptions = options;
        OnPropertyChanged(nameof(ShelfOptions));
        OnPropertyChanged(nameof(SelectedShelf));
    }

    private void SetMissing(CatalogueMissingFields field, bool missing) =>
        _filter.MissingFields = missing ? _filter.MissingFields | field : _filter.MissingFields & ~field;

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogueFilterViewModel.QualityBelow) &&
            _filter.QualityBelow is { } quality && !QualityOptions.Any(option => option.Value == quality))
        {
            BuildOptions();
        }

        RaiseSelections();
        OnPropertyChanged(nameof(Chips));
        OnPropertyChanged(nameof(NoResultsText));
        RaiseSmartShelfState();
    }

    private void OnShelvesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShelfSidebarViewModel.IsLoading) && !_shelves.IsLoading)
        {
            BuildShelfOptions();
            OnPropertyChanged(nameof(Chips));
            OnPropertyChanged(nameof(NoResultsText));
        }
    }

    private void RaiseSelections()
    {
        OnPropertyChanged(nameof(SelectedStatus));
        OnPropertyChanged(nameof(SelectedRating));
        OnPropertyChanged(nameof(SelectedAvailability));
        OnPropertyChanged(nameof(SelectedQuality));
        OnPropertyChanged(nameof(SelectedShelf));
        OnPropertyChanged(nameof(MissingAuthor));
        OnPropertyChanged(nameof(MissingIsbn));
        OnPropertyChanged(nameof(MissingCover));
        OnPropertyChanged(nameof(MissingYear));
    }

    private void RaiseSmartShelfState()
    {
        OnPropertyChanged(nameof(CanSaveSmartShelf));
        OnPropertyChanged(nameof(SmartShelfHint));
        OnPropertyChanged(nameof(HasSmartShelfHint));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
