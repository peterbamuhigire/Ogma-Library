namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Persisted, non-sensitive catalogue presentation state. <c>ShowAll</c> (Sept-23 Phase 10) lists the
/// whole result in the virtualised grid; older files without it default to showing all. The
/// extended criteria (quality, missing fields, needs OCR, possible duplicates) also default to
/// "not set" when absent, so older files keep loading.
/// </summary>
/// <param name="View">The catalogue view name.</param>
/// <param name="TitleSearch">The title search term.</param>
/// <param name="AuthorSearch">The author search term.</param>
/// <param name="StatusFilter">The status filter.</param>
/// <param name="MinRating">The minimum rating.</param>
/// <param name="MaxRating">The maximum rating.</param>
/// <param name="AvailabilityFilter">The availability filter.</param>
/// <param name="SelectedShelfId">The selected shelf.</param>
/// <param name="SortField">The sort field name.</param>
/// <param name="SortAscending">The sort direction.</param>
/// <param name="CurrentPage">The current page.</param>
/// <param name="ShowAll">Whether the whole result is listed.</param>
/// <param name="QualityBelow">Only books whose quality score is below this value.</param>
/// <param name="MissingFields">The <see cref="CatalogueMissingFields"/> flag names that must be missing.</param>
/// <param name="NeedsOcrOnly">Only books that need OCR.</param>
/// <param name="PossibleDuplicatesOnly">Only possible duplicates.</param>
public sealed record CatalogueViewState(
    string View,
    string? TitleSearch,
    string? AuthorSearch,
    int? StatusFilter,
    int? MinRating,
    int? MaxRating,
    bool? AvailabilityFilter,
    string? SelectedShelfId,
    string SortField,
    bool SortAscending,
    int CurrentPage,
    bool ShowAll = true,
    double? QualityBelow = null,
    string? MissingFields = null,
    bool NeedsOcrOnly = false,
    bool PossibleDuplicatesOnly = false);

/// <summary>Stores the user's catalogue view state across application restarts.</summary>
public interface ICatalogueViewStateStore
{
    /// <summary>Loads the last valid state, or <see langword="null"/> when none exists.</summary>
    Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists the supplied non-sensitive state.</summary>
    Task SaveAsync(CatalogueViewState state, CancellationToken cancellationToken = default);
}
