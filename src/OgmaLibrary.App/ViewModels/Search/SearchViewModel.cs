using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.App.ViewModels.Search;

/// <summary>
/// View model for Phase 10 global catalogue/full-text search.
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISemanticSearchService _searchService;
    private readonly IReaderNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly string _searchIconPath = IconCatalog.GetAvaresPath("ic_search_global") ?? string.Empty;
    private readonly string _resultBookIconPath = IconCatalog.GetAvaresPath("ic_search_result_book") ?? string.Empty;
    private CancellationTokenSource? _debounceCts;
    private string? _query;
    private SearchResultItem? _selectedResult;
    private bool _isSearching;
    private int _searchVersion;
    private string? _statusText;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchViewModel"/>.
    /// </summary>
    public SearchViewModel(
        ISemanticSearchService searchService,
        IReaderNavigationService navigation,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(searchService);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _searchService = searchService;
        _navigation = navigation;
        _localization = localization;
        _statusText = _localization["Search.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Search query text.</summary>
    public string? Query
    {
        get => _query;
        set
        {
            if (_query != value)
            {
                _query = value;
                OnPropertyChanged();
                ScheduleSearch();
            }
        }
    }

    /// <summary>Search results shown by the view.</summary>
    public ObservableCollection<SearchResultItem> Results { get; } = [];

    /// <summary>Currently selected result.</summary>
    public SearchResultItem? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult != value)
            {
                _selectedResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOpenSelected));
            }
        }
    }

    /// <summary>True while a debounced search is running.</summary>
    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (_isSearching != value)
            {
                _isSearching = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Status text for screen readers and compact UI feedback.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether the selected result can be opened.</summary>
    public bool CanOpenSelected => SelectedResult is not null;

    /// <summary>Localized search box watermark.</summary>
    public string PlaceholderText => _localization["Search.Placeholder"];

    /// <summary>Localized label for the search panel.</summary>
    public string PanelLabel => _localization["Search.Panel.Label"];

    /// <summary>Localized label for the open action.</summary>
    public string OpenSelectedLabel => _localization["Search.OpenSelected"];

    /// <summary>Icon path for global search.</summary>
    public string SearchIconPath => _searchIconPath;

    /// <summary>Icon path for search-result book rows.</summary>
    public string ResultBookIconPath => _resultBookIconPath;

    /// <summary>Runs search immediately using the current query.</summary>
    public async Task SearchNowAsync(CancellationToken cancellationToken = default)
    {
        _debounceCts?.Cancel();
        int version = Interlocked.Increment(ref _searchVersion);
        await RunSearchAsync(Query, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the selected result in the reader.</summary>
    public async Task OpenSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedResult is null)
        {
            return;
        }

        await _navigation
            .OpenReaderAsync(SelectedResult.BookId, SelectedResult.PageIndex, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _localization.CultureChanged -= OnCultureChanged;
    }

    private void ScheduleSearch()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        CancellationToken token = _debounceCts.Token;
        int version = Interlocked.Increment(ref _searchVersion);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), token).ConfigureAwait(false);
                await RunSearchAsync(Query, version, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task RunSearchAsync(string? query, int version, CancellationToken cancellationToken)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            if (version != Volatile.Read(ref _searchVersion))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Results.Clear();
                SelectedResult = null;
                StatusText = _localization["Search.Status.Ready"];
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => IsSearching = true);
        try
        {
            SemanticSearchResponse response = await _searchService
                .SearchAsync(trimmed, maxResults: 30, cancellationToken)
                .ConfigureAwait(false);
            if (version != Volatile.Read(ref _searchVersion) ||
                !string.Equals(trimmed, Query?.Trim() ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Results.Clear();
                foreach (SearchResultItem item in response.Results.Select(MapResult))
                {
                    Results.Add(item);
                }

                SelectedResult = Results.FirstOrDefault();
                StatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Search.Status.ResultsFormat"],
                    Results.Count);
            });
        }
        finally
        {
            if (version == Volatile.Read(ref _searchVersion))
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsSearching = false);
            }
        }
    }

    private static SearchResultItem MapResult(SemanticSearchResult result)
    {
        string subtitle = result.ConfidenceLabel.HasValue
            ? $"{result.ConfidenceLabel} confidence"
            : string.Empty;
        string snippet = result.Snippet ?? string.Empty;
        string matchLocations = result.MatchLocations is { Count: > 0 }
            ? string.Join(" · ", result.MatchLocations)
            : result.ExactFallback ? "Exact match" : "Semantic match";
        return new SearchResultItem(
            result.BookId,
            IconCatalog.GetAvaresPath("ic_search_result_book") ?? string.Empty,
            result.Title ?? "Untitled",
            subtitle,
            snippet,
            matchLocations,
            result.PageIndex,
            result.HybridScore ?? result.SemanticScore ?? 0);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(PanelLabel));
        OnPropertyChanged(nameof(OpenSelectedLabel));
        OnPropertyChanged(nameof(SearchIconPath));
        OnPropertyChanged(nameof(ResultBookIconPath));
        StatusText = Results.Count == 0
            ? _localization["Search.Status.Ready"]
            : string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Search.Status.ResultsFormat"],
                Results.Count);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Search result item for the Avalonia list.</summary>
public sealed record SearchResultItem(
    string BookId,
    string IconPath,
    string Title,
    string Subtitle,
    string Snippet,
    string MatchLocations,
    int? PageIndex,
    double Score);
