using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.App.ViewModels.Search;

/// <summary>
/// View model for the Search destination (Sept-23 Phase 13). Every request goes through
/// <see cref="IUnifiedSearchService"/> on a versioned, cancellable pipeline that runs its
/// continuations on the UI thread: a newer request cancels the older one, a stale response
/// is never applied, and a failure becomes a visible, logged error state with Retry (K41).
/// The mode chip and status text come from the measured response, never from assumptions.
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>Delay between the last keystroke and the search.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>How often an open query is refreshed while books are still being prepared.</summary>
    public static readonly TimeSpan LiveRefreshInterval = TimeSpan.FromSeconds(2);

    private const int MaxResults = 30;
    private static readonly TimeSpan LiveRefreshLimit = TimeSpan.FromMinutes(10);

    private readonly IUnifiedSearchService _searchService;
    private readonly IReaderNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly Func<string, CancellationToken, Task>? _focusBook;
    private readonly ILogger _logger;
    private readonly string _searchIconPath = IconCatalog.GetAvaresPath("ic_search_global") ?? string.Empty;
    private readonly string _resultBookIconPath = IconCatalog.GetAvaresPath("ic_search_result_book") ?? string.Empty;
    private CancellationTokenSource? _requestCts;
    private int _requestVersion;
    private string? _query;
    private SearchResultItem? _selectedResult;
    private bool _isSearching;
    private bool _hasError;
    private bool _isEmptyState;
    private SemanticSearchState? _semanticState;
    private SearchIndexCoverage? _coverage;
    private UnifiedSearchResponse? _lastResponse;
    private string? _statusText;

    /// <summary>Initializes a new instance of <see cref="SearchViewModel"/>.</summary>
    public SearchViewModel(
        IUnifiedSearchService searchService,
        IReaderNavigationService navigation,
        ILocalizationService localization,
        Func<string, CancellationToken, Task>? focusBook = null,
        ILogger<SearchViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(searchService);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _searchService = searchService;
        _navigation = navigation;
        _localization = localization;
        _focusBook = focusBook;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _statusText = _localization["Search.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Search query text. Setting it schedules a debounced search.</summary>
    public string? Query
    {
        get => _query;
        set
        {
            if (_query != value)
            {
                _query = value;
                OnPropertyChanged();
                _ = StartRequest(debounce: true);
            }
        }
    }

    /// <summary>Search results shown by the view, one per book.</summary>
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
                OnPropertyChanged(nameof(OpenSelectedLabel));
            }
        }
    }

    /// <summary>True while a request is waiting or running.</summary>
    public bool IsSearching
    {
        get => _isSearching;
        private set => SetField(ref _isSearching, value);
    }

    /// <summary>True when the latest request failed; the view offers Retry.</summary>
    public bool HasError
    {
        get => _hasError;
        private set => SetField(ref _hasError, value);
    }

    /// <summary>True when the latest request succeeded with no results.</summary>
    public bool IsEmptyState
    {
        get => _isEmptyState;
        private set => SetField(ref _isEmptyState, value);
    }

    /// <summary>Semantic state measured by the latest response, or null before any search.</summary>
    public SemanticSearchState? SemanticState
    {
        get => _semanticState;
        private set
        {
            if (_semanticState != value)
            {
                _semanticState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSemanticDegraded));
                OnPropertyChanged(nameof(SearchModeText));
                OnPropertyChanged(nameof(SearchModeToolTip));
                OnPropertyChanged(nameof(SearchModeIconPath));
            }
        }
    }

    /// <summary>Whether the latest response ran without semantic results.</summary>
    public bool IsSemanticDegraded => SemanticState is SemanticSearchState.Unavailable or SemanticSearchState.Preparing;

    /// <summary>Status text for screen readers and compact UI feedback.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>Index coverage, e.g. "15 of 17 books have searchable text; 2 need OCR".</summary>
    public string CoverageText => FormatCoverage(_coverage);

    /// <summary>Whether coverage is known.</summary>
    public bool HasCoverage => _coverage is { TotalBooks: > 0 };

    /// <summary>Whether the selected result can be opened.</summary>
    public bool CanOpenSelected => SelectedResult is not null;

    /// <summary>Localized search box watermark.</summary>
    public string PlaceholderText => _localization["Search.Placeholder"];

    /// <summary>Localized label for the search panel.</summary>
    public string PanelLabel => _localization["Search.Panel.Label"];

    /// <summary>Open action label: "Open at page N" when the selected result has a page hit.</summary>
    public string OpenSelectedLabel => SelectedResult?.PageIndex is int page
        ? string.Format(CultureInfo.CurrentCulture, _localization["Search.OpenAtPageFormat"], page + 1)
        : _localization["Search.OpenSelected"];

    /// <summary>Localized Retry label.</summary>
    public string RetryLabel => _localization["Search.Retry"];

    /// <summary>Mode chip text from the latest response.</summary>
    public string SearchModeText => SemanticState switch
    {
        SemanticSearchState.Active => _localization["Search.Mode.Semantic"],
        SemanticSearchState.Preparing => _localization["Search.Mode.Preparing"],
        SemanticSearchState.Unavailable => _localization["Search.Mode.Keyword"],
        _ => _localization["Search.Mode.Ready"],
    };

    /// <summary>Longer explanation of the mode for the chip's tooltip.</summary>
    public string SearchModeToolTip => SemanticState switch
    {
        SemanticSearchState.Active => _localization["Search.Mode.Semantic.Help"],
        SemanticSearchState.Preparing => _localization["Search.Mode.Preparing.Help"],
        _ => _localization["Search.Mode.Keyword.Help"],
    };

    /// <summary>Icon path for the mode chip.</summary>
    public string SearchModeIconPath => SemanticState == SemanticSearchState.Active
        ? IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty
        : IconCatalog.GetAvaresPath("ic_search_fulltext") ?? string.Empty;

    /// <summary>Icon path for global search.</summary>
    public string SearchIconPath => _searchIconPath;

    /// <summary>Icon path for search-result book rows.</summary>
    public string ResultBookIconPath => _resultBookIconPath;

    /// <summary>Runs the current query immediately and completes when its results are applied.</summary>
    public Task SearchNowAsync(CancellationToken cancellationToken = default) =>
        StartRequest(debounce: false, cancellationToken);

    /// <summary>Retries the current query after an error.</summary>
    public Task RetryAsync() => StartRequest(debounce: false);

    /// <summary>Loads index coverage for the header (called when the destination opens).</summary>
    public async Task RefreshCoverageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SearchIndexCoverage coverage = await Task.Run(
                    () => _searchService.GetCoverageAsync(cancellationToken),
                    cancellationToken)
                .ConfigureAwait(true);
            SetCoverage(coverage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLog.ViewModelOperationFailed(_logger, exception, nameof(SearchViewModel), "search.coverage");
        }
    }

    /// <summary>Opens the selected result in the reader, at its page hit when it has one.</summary>
    public async Task OpenSelectedAsync(CancellationToken cancellationToken = default)
    {
        SearchResultItem? selected = SelectedResult;
        if (selected is null)
        {
            return;
        }

        await _navigation
            .OpenReaderAsync(selected.BookId, selected.PageIndex, cancellationToken)
            .ConfigureAwait(true);
        if (_focusBook is not null)
        {
            await _focusBook(selected.BookId, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = null;
        _localization.CultureChanged -= OnCultureChanged;
    }

    private Task StartRequest(bool debounce, CancellationToken externalToken = default)
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = externalToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
            : new CancellationTokenSource();
        int version = ++_requestVersion;
        return RunRequestAsync(version, Query?.Trim() ?? string.Empty, debounce, _requestCts.Token);
    }

    private async Task RunRequestAsync(int version, string query, bool debounce, CancellationToken cancellationToken)
    {
        if (query.Length == 0)
        {
            Results.Clear();
            SelectedResult = null;
            HasError = false;
            IsEmptyState = false;
            IsSearching = false;
            _lastResponse = null;
            StatusText = _localization["Search.Status.Ready"];
            return;
        }

        IsSearching = true;
        try
        {
            if (debounce)
            {
                await Task.Delay(DebounceDelay, cancellationToken).ConfigureAwait(true);
            }

            DateTimeOffset started = DateTimeOffset.UtcNow;
            while (true)
            {
                UnifiedSearchResponse response = await Task.Run(
                        () => _searchService.SearchAsync(query, MaxResults, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(true);
                if (version != _requestVersion)
                {
                    return;
                }

                Apply(response);

                // While books are still being read, re-run the same query so results appear
                // as the library finishes processing, instead of a stale empty list (K41).
                if (response.Coverage.PendingBooks == 0 || DateTimeOffset.UtcNow - started > LiveRefreshLimit)
                {
                    break;
                }

                IsSearching = false;
                await Task.Delay(LiveRefreshInterval, cancellationToken).ConfigureAwait(true);
                if (version != _requestVersion)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer request or disposed; the newer request owns the state.
        }
        catch (Exception exception)
        {
            AppLog.ViewModelOperationFailed(_logger, exception, nameof(SearchViewModel), "search.query");
            if (version == _requestVersion)
            {
                Results.Clear();
                SelectedResult = null;
                IsEmptyState = false;
                HasError = true;
                StatusText = _localization["Search.Status.Error"];
            }
        }
        finally
        {
            if (version == _requestVersion)
            {
                IsSearching = false;
            }
        }
    }

    private void Apply(UnifiedSearchResponse response)
    {
        _lastResponse = response;
        List<SearchResultItem> mapped = response.Results.Select(MapResult).ToList();
        if (!mapped.Select(Identity).SequenceEqual(Results.Select(Identity), StringComparer.Ordinal))
        {
            string? selectedBookId = SelectedResult?.BookId;
            Results.Clear();
            foreach (SearchResultItem item in mapped)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault(item => item.BookId == selectedBookId) ?? Results.FirstOrDefault();
        }

        HasError = false;
        IsEmptyState = Results.Count == 0;
        SemanticState = response.Semantic == SemanticSearchState.NotApplicable
            ? SemanticState ?? SemanticSearchState.Unavailable
            : response.Semantic;
        SetCoverage(response.Coverage);
        StatusText = FormatStatus(response);
    }

    private static string Identity(SearchResultItem item) =>
        item.BookId + "|" + item.AutomationName + "|" + item.Snippet + "|" + item.MatchLocations;

    private void SetCoverage(SearchIndexCoverage coverage)
    {
        _coverage = coverage;
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(HasCoverage));
    }

    private string FormatStatus(UnifiedSearchResponse response)
    {
        string status;
        if (response.Results.Count == 0)
        {
            status = string.Format(CultureInfo.CurrentCulture, _localization["Search.Status.EmptyFormat"], response.Query.Text);
        }
        else if (response.UsedTypoTolerance)
        {
            status = string.Format(CultureInfo.CurrentCulture, _localization["Search.Status.TypoFormat"], response.Results.Count);
        }
        else
        {
            status = response.Results.Count == 1
                ? _localization["Search.Status.OneBook"]
                : string.Format(CultureInfo.CurrentCulture, _localization["Search.Status.BooksFormat"], response.Results.Count);
        }

        return response.Query.WasTruncated
            ? status + " " + string.Format(CultureInfo.CurrentCulture, _localization["Search.Status.TruncatedFormat"], SearchQueryParser.MaxQueryLength)
            : status;
    }

    private string FormatCoverage(SearchIndexCoverage? coverage)
    {
        if (coverage is not { TotalBooks: > 0 })
        {
            return string.Empty;
        }

        string text = coverage.NeedOcrBooks > 0
            ? string.Format(CultureInfo.CurrentCulture, _localization["Search.Coverage.WithOcrFormat"], coverage.SearchableBooks, coverage.TotalBooks, coverage.NeedOcrBooks)
            : string.Format(CultureInfo.CurrentCulture, _localization["Search.Coverage.Format"], coverage.SearchableBooks, coverage.TotalBooks);
        return coverage.PendingBooks > 0
            ? text + string.Format(CultureInfo.CurrentCulture, _localization["Search.Coverage.PendingSuffixFormat"], coverage.PendingBooks)
            : text;
    }

    private SearchResultItem MapResult(UnifiedSearchResult result)
    {
        var badges = new List<SearchResultBadge>();
        foreach (UnifiedMatch match in result.Matches
                     .GroupBy(match => match.Kind)
                     .Select(group => group.OrderBy(match => match.PageIndex ?? int.MaxValue).First())
                     .OrderBy(match => result.Matches.ToList().IndexOf(match)))
        {
            string label = BadgeLabel(match);
            badges.Add(new SearchResultBadge(
                BadgeIcon(match.Kind),
                label,
                string.Format(CultureInfo.CurrentCulture, _localization["Search.MatchLocation.AccessibleFormat"], label)));
        }

        if (result.IsOcrText)
        {
            // Sept-23 Phase 17 (task 8): text read from a scanned page by OCR is labelled.
            string ocrLabel = _localization["Search.Result.FromOcrText"];
            badges.Add(new SearchResultBadge(
                IconCatalog.GetAvaresPath("ic_filter_chip_page") ?? string.Empty,
                ocrLabel,
                string.Format(CultureInfo.CurrentCulture, _localization["Search.MatchLocation.AccessibleFormat"], ocrLabel)));
        }

        string subtitle = result.Author is { Length: > 0 } author
            ? string.Format(CultureInfo.CurrentCulture, _localization["Search.Result.ByFormat"], author)
            : string.Empty;
        string location = result.PrimaryMatch is { } primary ? LocationPhrase(primary) : _localization["Search.Location.Title"];
        string accessibleName = result.Author is { Length: > 0 } byAuthor
            ? string.Format(CultureInfo.CurrentCulture, _localization["Search.Result.AccessibleWithAuthorFormat"], result.Title, byAuthor, location)
            : string.Format(CultureInfo.CurrentCulture, _localization["Search.Result.AccessibleFormat"], result.Title, location);
        return new SearchResultItem(
            result.BookId,
            _resultBookIconPath,
            result.Title,
            subtitle,
            string.Empty,
            result.Snippet?.Text ?? string.Empty,
            string.Join(_localization["Search.Result.Separator"], badges.Select(badge => badge.Label)),
            badges,
            result.PageJumpTarget?.PageIndex,
            result.Score,
            result.PageJumpTarget,
            accessibleName,
            result.Snippet?.Spans ?? []);
    }

    private string LocationPhrase(UnifiedMatch match)
    {
        string phrase = match.Kind switch
        {
            UnifiedMatchKind.Page when match.PageIndex is int page =>
                string.Format(CultureInfo.CurrentCulture, _localization["Search.Location.PageFormat"], page + 1),
            _ => _localization[$"Search.Location.{match.Kind}"],
        };
        if (match.IsFuzzy)
        {
            phrase += " " + _localization["Search.Location.CloseSpelling"];
        }

        if (match.IsOcrText)
        {
            phrase += " " + _localization["Search.Location.OcrText"];
        }

        return phrase;
    }

    private string BadgeLabel(UnifiedMatch match)
    {
        string label = match.Kind switch
        {
            UnifiedMatchKind.Page when match.PageIndex is int page =>
                string.Format(CultureInfo.CurrentCulture, _localization["Search.Badge.PageFormat"], page + 1),
            UnifiedMatchKind.Page => _localization["Search.MatchLocation.TextPage"],
            UnifiedMatchKind.Note => _localization["Search.MatchLocation.NotePage"],
            UnifiedMatchKind.Isbn or UnifiedMatchKind.Year or UnifiedMatchKind.Shelf =>
                _localization[$"Search.Badge.{match.Kind}"],
            _ => _localization[$"Search.MatchLocation.{match.Kind}"],
        };
        return match.IsFuzzy
            ? label + " " + _localization["Search.Location.CloseSpelling"]
            : label;
    }

    private static string BadgeIcon(UnifiedMatchKind kind)
    {
        string key = kind switch
        {
            UnifiedMatchKind.Tag => "ic_filter_chip_tag",
            UnifiedMatchKind.Description => "ic_filter_chip_description",
            UnifiedMatchKind.Toc => "ic_filter_chip_toc",
            UnifiedMatchKind.Note => "ic_filter_chip_note",
            UnifiedMatchKind.Page => "ic_filter_chip_page",
            UnifiedMatchKind.Semantic => "ic_ai_advisor",
            _ => "ic_search_metadata",
        };
        return IconCatalog.GetAvaresPath(key) ?? string.Empty;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(PanelLabel));
        OnPropertyChanged(nameof(OpenSelectedLabel));
        OnPropertyChanged(nameof(RetryLabel));
        OnPropertyChanged(nameof(SearchModeText));
        OnPropertyChanged(nameof(SearchModeToolTip));
        OnPropertyChanged(nameof(SearchModeIconPath));
        OnPropertyChanged(nameof(CoverageText));
        if (_lastResponse is not null)
        {
            UnifiedSearchResponse response = _lastResponse;
            Apply(response);
        }
        else
        {
            StatusText = HasError ? _localization["Search.Status.Error"] : _localization["Search.Status.Ready"];
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>Search result item for the Avalonia list (one per book).</summary>
public sealed record SearchResultItem(
    string BookId,
    string IconPath,
    string Title,
    string Subtitle,
    string ConfidenceIconPath,
    string Snippet,
    string MatchLocations,
    IReadOnlyList<SearchResultBadge> MatchBadges,
    int? PageIndex,
    double Score,
    SearchPageJumpTarget? PageJumpTarget = null,
    string? AccessibleName = null,
    IReadOnlyList<SearchSnippetSpan>? SnippetSpans = null)
{
    /// <summary>Whether a confidence icon should be shown.</summary>
    public bool HasConfidence => !string.IsNullOrWhiteSpace(ConfidenceIconPath);

    /// <summary>Whether the author line has text.</summary>
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>Whether a text snippet is available.</summary>
    public bool HasSnippet => !string.IsNullOrWhiteSpace(Snippet);

    /// <summary>Readable name for assistive technology: "Title, by Author, matched on page N" (K14).</summary>
    public string AutomationName => AccessibleName ?? Title;

    /// <inheritdoc />
    public override string ToString() => AutomationName;
}

/// <summary>Localized match-location badge for a search result.</summary>
public sealed record SearchResultBadge(
    string IconPath,
    string Label,
    string AutomationLabel)
{
    /// <inheritdoc />
    public override string ToString() => AutomationLabel;
}
