using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Reader.Annotations;
using OgmaLibrary.Reader.Navigation;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>What the find bar is showing.</summary>
public enum FindBarState
{
    /// <summary>No query typed.</summary>
    Empty,

    /// <summary>A search is running.</summary>
    Searching,

    /// <summary>The search finished with no match.</summary>
    NoMatch,

    /// <summary>The search found at least one match.</summary>
    Matches,
}

/// <summary>Find bar, navigation history and the Contents tab (Phase 12, READ-006 and READ-002).</summary>
public sealed partial class ReaderViewModel
{
    private const int FindTabIndex = 5;

    private readonly IInDocumentSearchService? _search;
    private readonly NavigationHistory _history = new();
    private readonly List<(int Page, HighlightRect Rect)> _findOccurrences = [];
    private CancellationTokenSource? _findCts;
    private Task _findTask = Task.CompletedTask;
    private string _findQuery = string.Empty;
    private bool _isFindOpen;
    private FindBarState _findState = FindBarState.Empty;
    private string _findCountText = string.Empty;
    private int _findIndex = -1;
    private long _contentsSequence;
    private bool _isContentsLoading;

    /// <summary>Raised when the find box should take keyboard focus.</summary>
    public event EventHandler? FindFocusRequested;

    /// <summary>Raised when the page-number field should take keyboard focus and select its text.</summary>
    public event EventHandler? GoToPageFocusRequested;

    /// <summary>Delay between the last keystroke and the search starting.</summary>
    public TimeSpan FindDebounce { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>True when the find bar is shown.</summary>
    public bool IsFindOpen
    {
        get => _isFindOpen;
        private set => SetField(ref _isFindOpen, value);
    }

    /// <summary>The text being searched for. Changing it restarts a debounced, cancellable search.</summary>
    public string FindQuery
    {
        get => _findQuery;
        set
        {
            if (SetField(ref _findQuery, value ?? string.Empty))
            {
                StartFind();
            }
        }
    }

    /// <summary>The find bar state.</summary>
    public FindBarState FindState
    {
        get => _findState;
        private set
        {
            if (SetField(ref _findState, value))
            {
                OnPropertyChanged(nameof(HasFindResults));
            }
        }
    }

    /// <summary>The count or status line, for example "2 of 3" or "No matches".</summary>
    public string FindCountText
    {
        get => _findCountText;
        private set => SetField(ref _findCountText, value);
    }

    /// <summary>True when there is at least one match.</summary>
    public bool HasFindResults => FindState == FindBarState.Matches;

    /// <summary>Total number of matches across the book.</summary>
    public int FindMatchCount => _findOccurrences.Count;

    /// <summary>Matches grouped by page, shown in the side panel.</summary>
    public ObservableCollection<FindResultItem> FindResults { get; } = [];

    /// <summary>Highlights drawn over the current page.</summary>
    public ObservableCollection<FindHighlightItem> FindHighlights { get; } = [];

    /// <summary>Accessible name of the find box.</summary>
    public string FindLabel => _localization["Reader.Find.Label"];

    /// <summary>Placeholder in the find box.</summary>
    public string FindPlaceholder => _localization["Reader.Find.Placeholder"];

    /// <summary>Accessible label for the next-match button.</summary>
    public string FindNextLabel => _localization["Reader.Find.Next"];

    /// <summary>Accessible label for the previous-match button.</summary>
    public string FindPreviousLabel => _localization["Reader.Find.Previous"];

    /// <summary>Accessible label for the close button.</summary>
    public string FindCloseLabel => _localization["Reader.Find.Close"];

    /// <summary>Side-panel tab header for find results.</summary>
    public string FindResultsTabLabel => _localization["Reader.Find.ResultsTab"];

    /// <summary>Label for the history back button.</summary>
    public string HistoryBackLabel => _localization["Reader.History.Back"];

    /// <summary>Label for the history forward button.</summary>
    public string HistoryForwardLabel => _localization["Reader.History.Forward"];

    /// <summary>Side-panel tab header for the contents list.</summary>
    public string ContentsTabLabel => _localization["Reader.Contents.Tab"];

    /// <summary>Heading of the empty Contents state.</summary>
    public string ContentsEmptyText => _localization["Reader.Contents.Empty.Title"];

    /// <summary>Guidance in the empty Contents state.</summary>
    public string ContentsEmptyHint => _localization["Reader.Contents.Empty.Body"];

    /// <summary>The book's outline.</summary>
    public ObservableCollection<ContentsEntryItem> ContentsEntries { get; } = [];

    /// <summary>True when the book has no outline and loading has finished.</summary>
    public bool ShowContentsEmptyState => !_isContentsLoading && ContentsEntries.Count == 0;

    /// <summary>True when the history has an earlier position.</summary>
    public bool CanHistoryBack =>
        IsOpen && (_history.CanGoBack || (_history.Current >= 0 && _history.Current != CurrentPageIndex));

    /// <summary>True when the history has a later position.</summary>
    public bool CanHistoryForward => IsOpen && _history.CanGoForward;

    /// <summary>Shows the find bar and asks the view to focus the find box.</summary>
    public void OpenFind()
    {
        if (!IsOpen)
        {
            return;
        }

        IsFindOpen = true;
        RefreshFindHighlights();
        FindFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hides the find bar and removes the highlights; the query is kept.</summary>
    public void CloseFind()
    {
        IsFindOpen = false;
        FindHighlights.Clear();
    }

    /// <summary>Completes when the most recent find request has finished (used by tests).</summary>
    public Task WaitForFindAsync() => _findTask;

    /// <summary>Moves to the next match, wrapping at the end.</summary>
    public Task FindNextAsync() => StepFindAsync(1);

    /// <summary>Moves to the previous match, wrapping at the start.</summary>
    public Task FindPreviousAsync() => StepFindAsync(-1);

    /// <summary>Jumps to a page listed in the find results.</summary>
    /// <param name="result">The result row.</param>
    /// <returns>A task that completes when the page is shown.</returns>
    public Task GoToFindResultAsync(FindResultItem result)
    {
        ArgumentNullException.ThrowIfNull(result);
        int first = _findOccurrences.FindIndex(o => o.Page == result.PageIndex);
        return first < 0 ? Task.CompletedTask : GoToOccurrenceAsync(first);
    }

    /// <summary>Loads the outline of the open book. A missing or unreadable outline is an empty state.</summary>
    /// <returns>A task that completes when the list is updated.</returns>
    public async Task LoadContentsAsync()
    {
        long sequence = ++_contentsSequence;
        _isContentsLoading = true;
        OnPropertyChanged(nameof(ShowContentsEmptyState));

        var items = new List<ContentsEntryItem>();
        if (IsOpen && _sessions.CurrentRenderer is { } renderer)
        {
            try
            {
                IReadOnlyList<PdfOutlineEntry> outline = await renderer
                    .ReadOutlineAsync(CancellationToken.None).ConfigureAwait(true);
                foreach (PdfOutlineEntry entry in outline)
                {
                    if (entry.PageIndex >= 0 && entry.PageIndex < PageCount && !string.IsNullOrWhiteSpace(entry.Title))
                    {
                        items.Add(CreateContentsItem(entry));
                    }
                }
            }
            catch (Exception exception) when (IsRecoverableEngineFailure(exception) || exception is ArgumentException)
            {
                // An unreadable outline is the same as no outline: the tab shows its empty state.
                AppLog.ViewModelStepIgnored(_logger, exception, nameof(ReaderViewModel), "reader.contents");
            }
        }

        if (sequence != _contentsSequence)
        {
            return;
        }

        ContentsEntries.Clear();
        foreach (ContentsEntryItem item in items)
        {
            ContentsEntries.Add(item);
        }

        _isContentsLoading = false;
        OnPropertyChanged(nameof(ShowContentsEmptyState));
    }

    /// <summary>Jumps to an outline entry and records the jump in the history.</summary>
    /// <param name="entry">The outline entry.</param>
    /// <returns>A task that completes when the page is shown.</returns>
    public Task JumpToContentsEntryAsync(ContentsEntryItem entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return JumpAsync(entry.PageIndex);
    }

    /// <summary>Returns to the previous position in the history.</summary>
    /// <returns>A task that completes when the page is shown.</returns>
    public Task HistoryBackAsync()
    {
        if (!IsOpen)
        {
            return Task.CompletedTask;
        }

        // Page turns are not recorded; keep where the reader is so Forward can return here.
        if (_history.Current != CurrentPageIndex)
        {
            _history.Push(CurrentPageIndex);
        }

        int? page = _history.GoBack();
        RaiseHistoryChanged();
        return page is int target ? NavigateToAsync(target) : Task.CompletedTask;
    }

    /// <summary>Moves forward in the history after going back.</summary>
    /// <returns>A task that completes when the page is shown.</returns>
    public Task HistoryForwardAsync()
    {
        int? page = _history.GoForward();
        RaiseHistoryChanged();
        return page is int target ? NavigateToAsync(target) : Task.CompletedTask;
    }

    /// <summary>
    /// Handles a reader gesture: Ctrl/Cmd+F find, Ctrl/Cmd+G go to page, Alt+Left/Right or Cmd+[ and
    /// Cmd+] for history, F3 for the next match.
    /// </summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <returns><see langword="true"/> when the gesture was a reader command.</returns>
    public bool TryHandleReaderGesture(Key key, KeyModifiers modifiers)
    {
        if (!IsOpen)
        {
            return false;
        }

        bool primary = modifiers is KeyModifiers.Control or KeyModifiers.Meta;
        bool alt = modifiers == KeyModifiers.Alt;
        bool meta = modifiers == KeyModifiers.Meta;

        if (primary && key == Key.F)
        {
            OpenFind();
        }
        else if (primary && key == Key.G)
        {
            GoToPageFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else if ((alt && key == Key.Left) || (meta && key == Key.OemOpenBrackets))
        {
            UiActions.Run(HistoryBackAsync, "reader.history_back");
        }
        else if ((alt && key == Key.Right) || (meta && key == Key.OemCloseBrackets))
        {
            UiActions.Run(HistoryForwardAsync, "reader.history_forward");
        }
        else if (key == Key.F3 && IsFindOpen && modifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            UiActions.Run(
                modifiers == KeyModifiers.Shift ? FindPreviousAsync : FindNextAsync,
                "reader.find_step");
        }
        else
        {
            return false;
        }

        return true;
    }

    private Task JumpAsync(int pageIndex)
    {
        if (!IsOpen || pageIndex < 0 || pageIndex >= PageCount)
        {
            return _navigationLoop;
        }

        RecordJump(pageIndex);
        return NavigateToAsync(pageIndex);
    }

    private void RecordJump(int target)
    {
        if (_history.Current != CurrentPageIndex)
        {
            _history.Push(CurrentPageIndex);
        }

        if (target != CurrentPageIndex)
        {
            _history.Push(target);
        }

        RaiseHistoryChanged();
    }

    private void ResetHistoryAndFind()
    {
        _history.Clear();
        _history.Push(CurrentPageIndex);
        RaiseHistoryChanged();

        _findCts?.Cancel();
        ClearFindResults(FindBarState.Empty, string.Empty);
        _findQuery = string.Empty;
        OnPropertyChanged(nameof(FindQuery));
        ContentsEntries.Clear();
        UiActions.Run(LoadContentsAsync, "reader.contents");
    }

    private void RaiseHistoryChanged()
    {
        OnPropertyChanged(nameof(CanHistoryBack));
        OnPropertyChanged(nameof(CanHistoryForward));
    }

    private void StartFind()
    {
        _findCts?.Cancel();
        _findCts?.Dispose();
        _findCts = null;

        string query = _findQuery.Trim();
        if (query.Length == 0 || BookId is null || _search is null)
        {
            ClearFindResults(FindBarState.Empty, _localization["Reader.Find.Hint"]);
            _findTask = Task.CompletedTask;
            return;
        }

        var cts = new CancellationTokenSource();
        _findCts = cts;
        FindState = FindBarState.Searching;
        FindCountText = _localization["Reader.Find.Searching"];
        _findTask = RunFindAsync(BookId, query, cts.Token);
    }

    private async Task RunFindAsync(string bookId, string query, CancellationToken token)
    {
        try
        {
            if (FindDebounce > TimeSpan.Zero)
            {
                await Task.Delay(FindDebounce, token).ConfigureAwait(true);
            }

            IReadOnlyList<SearchMatch> matches = await _search!.SearchAsync(bookId, query, token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            ApplyFindResults(matches);
            if (_findOccurrences.Count > 0)
            {
                await GoToOccurrenceAsync(FirstOccurrenceFromCurrentPage()).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer query or closed: the newer request owns the state.
        }
        catch (Exception exception) when (IsRecoverableEngineFailure(exception) || exception is ArgumentException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(ReaderViewModel), "reader.find");
            if (!token.IsCancellationRequested)
            {
                ClearFindResults(FindBarState.NoMatch, _localization["Reader.Find.Failed"]);
            }
        }
    }

    private void ApplyFindResults(IReadOnlyList<SearchMatch> matches)
    {
        _findOccurrences.Clear();
        FindResults.Clear();
        foreach (SearchMatch match in matches.OrderBy(m => m.PageIndex))
        {
            foreach (HighlightRect rect in match.Highlights)
            {
                _findOccurrences.Add((match.PageIndex, rect));
            }

            FindResults.Add(new FindResultItem(
                match.PageIndex,
                string.Format(CultureInfo.CurrentCulture, _localization["Reader.Find.PageResult"], match.PageIndex + 1),
                match.ContextSnippet,
                match.Highlights.Count));
        }

        OnPropertyChanged(nameof(FindMatchCount));
        _findIndex = -1;
        if (_findOccurrences.Count == 0)
        {
            ClearFindResults(FindBarState.NoMatch, _localization["Reader.Find.NoMatch"]);
            return;
        }

        FindState = FindBarState.Matches;
        SelectedSidebarTabIndex = FindTabIndex;
        IsSidePanelOpen = true;
    }

    private void ClearFindResults(FindBarState state, string text)
    {
        _findOccurrences.Clear();
        FindResults.Clear();
        FindHighlights.Clear();
        _findIndex = -1;
        OnPropertyChanged(nameof(FindMatchCount));
        FindState = state;
        FindCountText = text;
    }

    private int FirstOccurrenceFromCurrentPage()
    {
        int index = _findOccurrences.FindIndex(o => o.Page >= CurrentPageIndex);
        return index < 0 ? 0 : index;
    }

    private Task StepFindAsync(int direction)
    {
        int count = _findOccurrences.Count;
        if (count == 0)
        {
            return Task.CompletedTask;
        }

        int next = _findIndex < 0 ? 0 : (((_findIndex + direction) % count) + count) % count;
        return GoToOccurrenceAsync(next);
    }

    private async Task GoToOccurrenceAsync(int index)
    {
        if (index < 0 || index >= _findOccurrences.Count)
        {
            return;
        }

        _findIndex = index;
        int page = _findOccurrences[index].Page;
        FindCountText = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Reader.Find.Count"],
            index + 1,
            _findOccurrences.Count);
        if (page != CurrentPageIndex)
        {
            await JumpAsync(page).ConfigureAwait(true);
        }

        RefreshFindHighlights();
    }

    /// <summary>Recomputes the page-space rectangles; called on page, zoom and rotation changes.</summary>
    private void RefreshFindHighlights()
    {
        FindHighlights.Clear();
        if (!IsFindOpen || _findIndex < 0)
        {
            return;
        }

        for (int i = 0; i < _findOccurrences.Count; i++)
        {
            (int page, HighlightRect rect) = _findOccurrences[i];
            if (page != CurrentPageIndex)
            {
                continue;
            }

            ScreenRect screen = AnnotationRenderHelper.ToScreenRect(
                new AnnotationRegion(
                    page,
                    rect.Left,
                    rect.Top,
                    Math.Max(0.0, rect.Right - rect.Left),
                    Math.Max(0.0, rect.Bottom - rect.Top)),
                BasePageSurfaceWidth,
                BasePageSurfaceHeight,
                PageRotationDegrees,
                OverlayZoomFactor);
            FindHighlights.Add(new FindHighlightItem(screen.X, screen.Y, screen.Width, screen.Height, i == _findIndex));
        }
    }

    private ContentsEntryItem CreateContentsItem(PdfOutlineEntry entry)
    {
        string title = entry.Title.Trim();
        return new ContentsEntryItem(
            title,
            entry.PageIndex,
            Math.Max(0, entry.Level),
            string.Format(
                CultureInfo.CurrentCulture,
                _localization["Reader.Contents.EntryLabel"],
                title,
                entry.PageIndex + 1));
    }

    private void RaiseFindLocalizedProperties()
    {
        OnPropertyChanged(nameof(FindLabel));
        OnPropertyChanged(nameof(FindPlaceholder));
        OnPropertyChanged(nameof(FindNextLabel));
        OnPropertyChanged(nameof(FindPreviousLabel));
        OnPropertyChanged(nameof(FindCloseLabel));
        OnPropertyChanged(nameof(FindResultsTabLabel));
        OnPropertyChanged(nameof(HistoryBackLabel));
        OnPropertyChanged(nameof(HistoryForwardLabel));
        OnPropertyChanged(nameof(ContentsTabLabel));
        OnPropertyChanged(nameof(ContentsEmptyText));
        OnPropertyChanged(nameof(ContentsEmptyHint));
    }
}

/// <summary>One page of find results.</summary>
/// <param name="PageIndex">Zero-based page.</param>
/// <param name="PageLabel">Localized "Page N".</param>
/// <param name="Snippet">The matched words.</param>
/// <param name="MatchCount">Number of matches on the page.</param>
public sealed record FindResultItem(int PageIndex, string PageLabel, string Snippet, int MatchCount);

/// <summary>A highlight rectangle in page-surface pixels.</summary>
/// <param name="X">Left offset.</param>
/// <param name="Y">Top offset.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="IsCurrent">True for the match the find bar is on.</param>
public sealed record FindHighlightItem(double X, double Y, double Width, double Height, bool IsCurrent)
{
    /// <summary>Absolute position inside the page host.</summary>
    public Thickness Margin => new(X, Y, 0, 0);
}

/// <summary>One entry of the book's outline.</summary>
/// <param name="Title">The heading.</param>
/// <param name="PageIndex">Zero-based target page.</param>
/// <param name="Level">Nesting level, zero for top level.</param>
/// <param name="AccessibleLabel">Screen-reader label.</param>
public sealed record ContentsEntryItem(string Title, int PageIndex, int Level, string AccessibleLabel)
{
    /// <summary>The one-based page number shown on the right.</summary>
    public string PageText => (PageIndex + 1).ToString(CultureInfo.CurrentCulture);

    /// <summary>Indentation for nested entries.</summary>
    public Thickness Indent => new(Math.Min(Level, 6) * 14, 0, 0, 0);
}
