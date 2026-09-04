namespace OgmaLibrary.Application.Catalogue;

/// <summary>Persisted, non-sensitive catalogue presentation state.</summary>
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
    int CurrentPage);

/// <summary>Stores the user's catalogue view state across application restarts.</summary>
public interface ICatalogueViewStateStore
{
    /// <summary>Loads the last valid state, or <see langword="null"/> when none exists.</summary>
    Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists the supplied non-sensitive state.</summary>
    Task SaveAsync(CatalogueViewState state, CancellationToken cancellationToken = default);
}
