using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>One catalogue book offered by the split-view picker.</summary>
/// <param name="BookId">Catalogue identity.</param>
/// <param name="Title">Display title.</param>
/// <param name="Authors">Display authors, comma separated; may be empty.</param>
public sealed record BookPickerItem(string BookId, string Title, string Authors)
{
    /// <summary>Accessible, single-line description.</summary>
    public string Display => Authors.Length == 0 ? Title : $"{Title} - {Authors}";
}

/// <summary>Search-as-you-type picker over the catalogue (Sept-23 Phase 12, READ-012).</summary>
public sealed class BookPickerViewModel : INotifyPropertyChanged
{
    private const int MaxSuggestions = 12;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    private readonly ICatalogueReadModel _catalogue;
    private readonly ILocalizationService _localization;
    private CancellationTokenSource? _typing;
    private string _query = string.Empty;
    private bool _suppress;

    /// <summary>Initializes a new instance of <see cref="BookPickerViewModel"/>.</summary>
    public BookPickerViewModel(ICatalogueReadModel catalogue, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(localization);
        _catalogue = catalogue;
        _localization = localization;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user chooses a book.</summary>
    public event EventHandler<BookPickerItem>? BookPicked;

    /// <summary>The matching catalogue books.</summary>
    public ObservableCollection<BookPickerItem> Results { get; } = [];

    /// <summary>True when there is something to choose.</summary>
    public bool HasResults => Results.Count > 0;

    /// <summary>True when a non-empty search found nothing.</summary>
    public bool IsEmptyVisible => !_suppress && _query.Trim().Length > 0 && Results.Count == 0;

    /// <summary>Localized watermark and accessible name.</summary>
    public string Label => _localization["SplitView.Picker.Label"];

    /// <summary>Localized empty-result message.</summary>
    public string EmptyText => _localization["SplitView.Picker.Empty"];

    /// <summary>The text typed so far; changing it schedules a debounced search.</summary>
    public string Query
    {
        get => _query;
        set
        {
            string normalized = value ?? string.Empty;
            if (_query == normalized)
            {
                return;
            }

            _query = normalized;
            OnPropertyChanged();
            if (!_suppress)
            {
                ScheduleSearch();
            }
        }
    }

    /// <summary>Runs the search for the current <see cref="Query"/> now.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        string text = _query.Trim();
        var found = new List<BookPickerItem>();
        if (text.Length > 0)
        {
            await CollectAsync(new CatalogueFilter(TitleContains: text, MaxResults: MaxSuggestions), found, cancellationToken)
                .ConfigureAwait(true);
            if (found.Count < MaxSuggestions)
            {
                await CollectAsync(new CatalogueFilter(AuthorContains: text, MaxResults: MaxSuggestions), found, cancellationToken)
                    .ConfigureAwait(true);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        Results.Clear();
        foreach (BookPickerItem item in found.Take(MaxSuggestions))
        {
            Results.Add(item);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmptyVisible));
    }

    /// <summary>Chooses <paramref name="item"/>: closes the list and raises <see cref="BookPicked"/>.</summary>
    public void Pick(BookPickerItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _typing?.Cancel();
        _suppress = true;
        try
        {
            Query = item.Title;
            Results.Clear();
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmptyVisible));
        BookPicked?.Invoke(this, item);
    }

    private async Task CollectAsync(CatalogueFilter filter, List<BookPickerItem> into, CancellationToken ct)
    {
        await foreach (BookSummaryProjection book in _catalogue.GetBookSummariesAsync(filter, ct).ConfigureAwait(true))
        {
            if (into.Exists(existing => existing.BookId == book.BookId))
            {
                continue;
            }

            into.Add(new BookPickerItem(
                book.BookId,
                string.IsNullOrWhiteSpace(book.Title) ? book.BookId : book.Title,
                string.Join(", ", book.Authors)));
            if (into.Count >= MaxSuggestions)
            {
                break;
            }
        }
    }

    private void ScheduleSearch()
    {
        _typing?.Cancel();
        _typing?.Dispose();
        var cts = new CancellationTokenSource();
        _typing = cts;
        UiActions.Run(() => SearchAfterPauseAsync(cts.Token), "reader.split.picker");
    }

    private async Task SearchAfterPauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Debounce, cancellationToken).ConfigureAwait(true);
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke superseded this search.
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
