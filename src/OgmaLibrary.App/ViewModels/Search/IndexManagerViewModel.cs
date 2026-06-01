using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.App.ViewModels.Search;

/// <summary>
/// View model for the Phase 10 Index Manager dashboard.
/// </summary>
public sealed class IndexManagerViewModel : INotifyPropertyChanged, IObserver<IndexStatusUpdate>, IDisposable
{
    private readonly IIndexManagerService _indexManager;
    private readonly ILocalizationService _localization;
    private readonly IDisposable _subscription;
    private CancellationTokenSource? _rebuildCts;
    private bool _isRebuilding;
    private bool _isRebuildConfirmationOpen;
    private string? _statusText;

    /// <summary>
    /// Initializes a new instance of <see cref="IndexManagerViewModel"/>.
    /// </summary>
    public IndexManagerViewModel(
        IIndexManagerService indexManager,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(indexManager);
        ArgumentNullException.ThrowIfNull(localization);

        _indexManager = indexManager;
        _localization = localization;
        _statusText = _localization["IndexManager.Status.Ready"];
        _subscription = _indexManager.Events.Subscribe(this);
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Per-book index rows.</summary>
    public ObservableCollection<BookIndexStatusItem> Books { get; } = [];

    /// <summary>Current index errors surfaced in the dashboard.</summary>
    public ObservableCollection<string> ErrorItems { get; } = [];

    /// <summary>Total active books.</summary>
    public int TotalBooks { get; private set; }

    /// <summary>Indexed active books.</summary>
    public int IndexedBooks { get; private set; }

    /// <summary>Books with failed extraction status.</summary>
    public int FailedBooks { get; private set; }

    /// <summary>Pages waiting for future OCR.</summary>
    public int PendingOcrPages { get; private set; }

    /// <summary>Failed extracted pages.</summary>
    public int FailedExtractionPages { get; private set; }

    /// <summary>Search chunks currently stored.</summary>
    public int SearchChunkCount { get; private set; }

    /// <summary>Approximate index text size.</summary>
    public long IndexSizeBytes { get; private set; }

    /// <summary>Whether the FTS index passed integrity check.</summary>
    public bool IntegrityHealthy { get; private set; } = true;

    /// <summary>True while rebuild is running.</summary>
    public bool IsRebuilding
    {
        get => _isRebuilding;
        private set
        {
            if (_isRebuilding != value)
            {
                _isRebuilding = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRebuild));
                OnPropertyChanged(nameof(CanCancelRebuild));
                OnPropertyChanged(nameof(IsRebuildProgressVisible));
            }
        }
    }

    /// <summary>Whether rebuild confirmation is visible.</summary>
    public bool IsRebuildConfirmationOpen
    {
        get => _isRebuildConfirmationOpen;
        private set
        {
            if (_isRebuildConfirmationOpen != value)
            {
                _isRebuildConfirmationOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Status text for UI and screen-reader announcements.</summary>
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

    /// <summary>Rebuild action availability.</summary>
    public bool CanRebuild => !IsRebuilding;

    /// <summary>Cancel action availability.</summary>
    public bool CanCancelRebuild => IsRebuilding;

    /// <summary>Whether the rebuild progress indicator is visible.</summary>
    public bool IsRebuildProgressVisible => IsRebuilding;

    /// <summary>Whether dashboard errors should be shown.</summary>
    public bool HasErrors => ErrorItems.Count > 0;

    /// <summary>Localized panel label.</summary>
    public string PanelLabel => _localization["IndexManager.Panel.Label"];

    /// <summary>Localized rebuild label.</summary>
    public string RebuildLabel => _localization["IndexManager.Rebuild"];

    /// <summary>Localized cancel label.</summary>
    public string CancelLabel => _localization["IndexManager.Cancel"];

    /// <summary>Localized confirmation prompt.</summary>
    public string RebuildConfirmationText => _localization["IndexManager.Rebuild.ConfirmText"];

    /// <summary>Localized confirmation action label.</summary>
    public string ConfirmRebuildLabel => _localization["IndexManager.Rebuild.Confirm"];

    /// <summary>Localized index-size summary.</summary>
    public string SizeSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.SizeFormat"],
        FormatBytes(IndexSizeBytes));

    /// <summary>Localized failed page count summary.</summary>
    public string FailedPagesSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.FailedPagesFormat"],
        FailedExtractionPages);

    /// <summary>Localized integrity summary.</summary>
    public string IntegritySummary => IntegrityHealthy
        ? _localization["IndexManager.Summary.IntegrityHealthy"]
        : _localization["IndexManager.Summary.IntegrityFailed"];

    /// <summary>Localized indexed count summary.</summary>
    public string IndexedSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.IndexedFormat"],
        IndexedBooks);

    /// <summary>Localized failed count summary.</summary>
    public string FailedSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.FailedFormat"],
        FailedBooks);

    /// <summary>Localized pending OCR count summary.</summary>
    public string PendingOcrSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.OcrFormat"],
        PendingOcrPages);

    /// <summary>Localized chunk count summary.</summary>
    public string ChunkSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.ChunksFormat"],
        SearchChunkCount);

    /// <summary>Opens rebuild confirmation.</summary>
    public void RequestRebuildConfirmation()
    {
        if (!IsRebuilding)
        {
            IsRebuildConfirmationOpen = true;
        }
    }

    /// <summary>Dismisses rebuild confirmation.</summary>
    public void CancelRebuildConfirmation() => IsRebuildConfirmationOpen = false;

    /// <summary>Loads a fresh index status snapshot.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IndexManagerStatus status = await _indexManager
            .GetStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        await ApplyStatusAsync(status).ConfigureAwait(false);
    }

    /// <summary>Starts a rebuild.</summary>
    public async Task ConfirmRebuildAsync(CancellationToken cancellationToken = default)
    {
        if (IsRebuilding)
        {
            return;
        }

        IsRebuildConfirmationOpen = false;
        _rebuildCts?.Dispose();
        _rebuildCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            IsRebuilding = true;
            await _indexManager.RebuildAsync(_rebuildCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusText = _localization["IndexManager.Status.Cancelled"]);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = _localization["IndexManager.Status.RebuildFailed"];
                ErrorItems.Add(ex.Message);
                OnPropertyChanged(nameof(HasErrors));
            });
            throw;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsRebuilding = false);
        }
    }

    /// <summary>Starts a rebuild without interactive confirmation.</summary>
    public Task RebuildAsync(CancellationToken cancellationToken = default) =>
        ConfirmRebuildAsync(cancellationToken);

    /// <summary>Cancels a running rebuild.</summary>
    public void CancelRebuild() => _rebuildCts?.Cancel();

    /// <inheritdoc />
    public void OnCompleted()
    {
    }

    /// <inheritdoc />
    public void OnError(Exception error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = error.Message;
            ErrorItems.Add(error.Message);
            OnPropertyChanged(nameof(HasErrors));
        });
    }

    /// <inheritdoc />
    public void OnNext(IndexStatusUpdate value)
    {
        switch (value)
        {
            case IndexStatusUpdate.StatusChanged changed:
                _ = ApplyStatusAsync(changed.Status);
                break;
            case IndexStatusUpdate.RebuildStarted:
                Dispatcher.UIThread.Post(() =>
                {
                    IsRebuilding = true;
                    ErrorItems.Clear();
                    OnPropertyChanged(nameof(HasErrors));
                    StatusText = _localization["IndexManager.Status.Rebuilding"];
                });
                break;
            case IndexStatusUpdate.RebuildCompleted completed:
                Dispatcher.UIThread.Post(() =>
                {
                    IsRebuilding = false;
                    StatusText = completed.Result.Completed
                    ? _localization["IndexManager.Status.RebuildComplete"]
                    : completed.Result.ErrorMessage ?? _localization["IndexManager.Status.RebuildFailed"];
                    if (!completed.Result.Completed && !string.IsNullOrWhiteSpace(completed.Result.ErrorMessage))
                    {
                        ErrorItems.Add(completed.Result.ErrorMessage);
                        OnPropertyChanged(nameof(HasErrors));
                    }
                });
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _rebuildCts?.Cancel();
        _rebuildCts?.Dispose();
        _subscription.Dispose();
        _localization.CultureChanged -= OnCultureChanged;
    }

    private async Task ApplyStatusAsync(IndexManagerStatus status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TotalBooks = status.TotalBooks;
            IndexedBooks = status.IndexedBooks;
            FailedBooks = status.FailedBooks;
            PendingOcrPages = status.PendingOcrPages;
            FailedExtractionPages = status.FailedExtractionPages;
            SearchChunkCount = status.SearchChunkCount;
            IndexSizeBytes = status.IndexSizeBytes;
            IntegrityHealthy = status.Integrity.IsHealthy;

            Books.Clear();
            foreach (BookIndexStatusItem book in status.Books)
            {
                Books.Add(book);
            }

            ErrorItems.Clear();
            if (!status.Integrity.IsHealthy && !string.IsNullOrWhiteSpace(status.Integrity.ErrorMessage))
            {
                ErrorItems.Add(status.Integrity.ErrorMessage);
            }

            foreach (BookIndexStatusItem book in status.Books.Where(book =>
                book.Status == SearchBookIndexStatus.Failed || book.FailedPageCount > 0))
            {
                ErrorItems.Add(string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "{0}: {1} failed pages",
                    book.Title ?? book.BookId,
                    book.FailedPageCount));
            }

            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["IndexManager.Status.CountsFormat"],
                IndexedBooks,
                TotalBooks,
                SearchChunkCount);
            RaiseStatusProperties();
        });
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PanelLabel));
        OnPropertyChanged(nameof(RebuildLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(RebuildConfirmationText));
        OnPropertyChanged(nameof(ConfirmRebuildLabel));
        OnPropertyChanged(nameof(IndexedSummary));
        OnPropertyChanged(nameof(FailedSummary));
        OnPropertyChanged(nameof(PendingOcrSummary));
        OnPropertyChanged(nameof(ChunkSummary));
        OnPropertyChanged(nameof(SizeSummary));
        OnPropertyChanged(nameof(FailedPagesSummary));
        OnPropertyChanged(nameof(IntegritySummary));
    }

    private void RaiseStatusProperties()
    {
        OnPropertyChanged(nameof(TotalBooks));
        OnPropertyChanged(nameof(IndexedBooks));
        OnPropertyChanged(nameof(FailedBooks));
        OnPropertyChanged(nameof(PendingOcrPages));
        OnPropertyChanged(nameof(FailedExtractionPages));
        OnPropertyChanged(nameof(SearchChunkCount));
        OnPropertyChanged(nameof(IndexSizeBytes));
        OnPropertyChanged(nameof(IntegrityHealthy));
        OnPropertyChanged(nameof(IndexedSummary));
        OnPropertyChanged(nameof(FailedSummary));
        OnPropertyChanged(nameof(PendingOcrSummary));
        OnPropertyChanged(nameof(ChunkSummary));
        OnPropertyChanged(nameof(SizeSummary));
        OnPropertyChanged(nameof(FailedPagesSummary));
        OnPropertyChanged(nameof(IntegritySummary));
        OnPropertyChanged(nameof(HasErrors));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0} B", bytes);
        }

        double kib = bytes / 1024d;
        if (kib < 1024)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:0.#} KiB", kib);
        }

        return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:0.#} MiB", kib / 1024d);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
