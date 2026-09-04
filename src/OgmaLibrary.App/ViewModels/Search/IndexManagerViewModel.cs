using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.App.ViewModels.Search;

/// <summary>
/// View model for the Phase 10 Index Manager dashboard.
/// </summary>
public sealed class IndexManagerViewModel : INotifyPropertyChanged, IObserver<IndexStatusUpdate>, IDisposable
{
    private readonly IIndexManagerService _indexManager;
    private readonly IEmbeddingErasureService _embeddingErasure;
    private readonly ILocalizationService _localization;
    private readonly TimeSpan _erasureConfirmationDelay;
    private readonly IDisposable _subscription;
    private readonly string _indexManagerIconPath = IconCatalog.GetAvaresPath("ic_index_manager") ?? string.Empty;
    private readonly string _rebuildIconPath = IconCatalog.GetAvaresPath("ic_index_rebuild") ?? string.Empty;
    private readonly string _cancelIconPath = IconCatalog.GetAvaresPath("ic_index_rebuild_cancel") ?? string.Empty;
    private readonly string _eraseEmbeddingsIconPath = IconCatalog.GetAvaresPath("ic_ai_privacy") ?? string.Empty;
    private readonly string _sizeIconPath = IconCatalog.GetAvaresPath("ic_index_size") ?? string.Empty;
    private CancellationTokenSource? _rebuildCts;
    private CancellationTokenSource? _erasureCountdownCts;
    private bool _isRebuilding;
    private bool _isErasingEmbeddings;
    private bool _isRebuildConfirmationOpen;
    private bool _isEmbeddingErasureConfirmationOpen;
    private bool _canConfirmEmbeddingErasure;
    private int _embeddingErasureCountdownSeconds;
    private string? _statusText;
    private IReadOnlyList<OcrJobStatusItem> _ocrJobs = [];
    private SmartShelfQueryStats _smartShelfStats = new(-1, RequiredIndexesHealthy: false, MissingIndexes: []);

    /// <summary>
    /// Initializes a new instance of <see cref="IndexManagerViewModel"/>.
    /// </summary>
    public IndexManagerViewModel(
        IIndexManagerService indexManager,
        IEmbeddingErasureService embeddingErasure,
        ILocalizationService localization,
        TimeSpan? erasureConfirmationDelay = null)
    {
        ArgumentNullException.ThrowIfNull(indexManager);
        ArgumentNullException.ThrowIfNull(embeddingErasure);
        ArgumentNullException.ThrowIfNull(localization);

        _indexManager = indexManager;
        _embeddingErasure = embeddingErasure;
        _localization = localization;
        _erasureConfirmationDelay = erasureConfirmationDelay ?? TimeSpan.FromSeconds(3);
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

    /// <summary>Current OCR job rows surfaced in the dashboard.</summary>
    public ObservableCollection<OcrJobStatusDisplayItem> OcrJobs { get; } = [];

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

    /// <summary>Queued or running OCR jobs.</summary>
    public int ActiveOcrJobs { get; private set; }

    /// <summary>Search chunks currently stored.</summary>
    public int SearchChunkCount { get; private set; }

    /// <summary>Vectors whose source fingerprint no longer matches local text.</summary>
    public int StaleEmbeddingCount { get; private set; }

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
                OnPropertyChanged(nameof(CanEraseEmbeddings));
                OnPropertyChanged(nameof(IsRebuildProgressVisible));
            }
        }
    }

    /// <summary>True while embedding erasure is running.</summary>
    public bool IsErasingEmbeddings
    {
        get => _isErasingEmbeddings;
        private set
        {
            if (_isErasingEmbeddings != value)
            {
                _isErasingEmbeddings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEraseEmbeddings));
                OnPropertyChanged(nameof(CanConfirmEmbeddingErasure));
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

    /// <summary>Whether embedding-erasure confirmation is visible.</summary>
    public bool IsEmbeddingErasureConfirmationOpen
    {
        get => _isEmbeddingErasureConfirmationOpen;
        private set
        {
            if (_isEmbeddingErasureConfirmationOpen != value)
            {
                _isEmbeddingErasureConfirmationOpen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirmEmbeddingErasure));
            }
        }
    }

    /// <summary>Seconds remaining before embedding erasure can be confirmed.</summary>
    public int EmbeddingErasureCountdownSeconds
    {
        get => _embeddingErasureCountdownSeconds;
        private set
        {
            if (_embeddingErasureCountdownSeconds != value)
            {
                _embeddingErasureCountdownSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmbeddingErasureCountdownText));
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

    /// <summary>Embedding erasure action availability.</summary>
    public bool CanEraseEmbeddings => !IsRebuilding && !IsErasingEmbeddings;

    /// <summary>Embedding erasure confirmation availability.</summary>
    public bool CanConfirmEmbeddingErasure =>
        IsEmbeddingErasureConfirmationOpen &&
        _canConfirmEmbeddingErasure &&
        !IsErasingEmbeddings;

    /// <summary>Whether the rebuild progress indicator is visible.</summary>
    public bool IsRebuildProgressVisible => IsRebuilding;

    /// <summary>Whether dashboard errors should be shown.</summary>
    public bool HasErrors => ErrorItems.Count > 0;

    /// <summary>Whether OCR job status should be shown.</summary>
    public bool HasOcrJobs => OcrJobs.Count > 0;

    /// <summary>Whether smart-shelf indexes are available.</summary>
    public bool SmartShelfIndexesHealthy => _smartShelfStats.RequiredIndexesHealthy;

    /// <summary>Localized panel label.</summary>
    public string PanelLabel => _localization["IndexManager.Panel.Label"];

    /// <summary>Localized rebuild label.</summary>
    public string RebuildLabel => _localization["IndexManager.Rebuild"];

    /// <summary>Localized cancel label.</summary>
    public string CancelLabel => _localization["IndexManager.Cancel"];

    /// <summary>Icon path for the Index Manager identity.</summary>
    public string IndexManagerIconPath => _indexManagerIconPath;

    /// <summary>Icon path for rebuild action.</summary>
    public string RebuildIconPath => _rebuildIconPath;

    /// <summary>Icon path for cancel rebuild action.</summary>
    public string CancelIconPath => _cancelIconPath;

    /// <summary>Icon path for embedding erasure action.</summary>
    public string EraseEmbeddingsIconPath => _eraseEmbeddingsIconPath;

    /// <summary>Icon path for index-size status.</summary>
    public string SizeIconPath => _sizeIconPath;

    /// <summary>Localized confirmation prompt.</summary>
    public string RebuildConfirmationText => _localization["IndexManager.Rebuild.ConfirmText"];

    /// <summary>Localized confirmation action label.</summary>
    public string ConfirmRebuildLabel => _localization["IndexManager.Rebuild.Confirm"];

    /// <summary>Localized embedding erasure action label.</summary>
    public string EraseEmbeddingsLabel => _localization["IndexManager.Embeddings.Erase"];

    /// <summary>Localized embedding erasure confirmation prompt.</summary>
    public string EmbeddingErasureConfirmationText => _localization["IndexManager.Embeddings.ConfirmText"];

    /// <summary>Localized embedding erasure confirmation action label.</summary>
    public string ConfirmEmbeddingErasureLabel => _localization["IndexManager.Embeddings.Confirm"];

    /// <summary>Localized countdown text for embedding erasure confirmation.</summary>
    public string EmbeddingErasureCountdownText => EmbeddingErasureCountdownSeconds > 0
        ? string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["IndexManager.Embeddings.CountdownFormat"],
            EmbeddingErasureCountdownSeconds)
        : _localization["IndexManager.Embeddings.ReadyToConfirm"];

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

    /// <summary>Localized OCR job summary.</summary>
    public string OcrJobsSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.OcrJobs.ActiveFormat"],
        ActiveOcrJobs);

    /// <summary>Localized smart-shelf query timing summary.</summary>
    public string SmartShelfQuerySummary => _smartShelfStats.HasQuerySample
        ? string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["IndexManager.SmartShelves.QueryTimeFormat"],
            _smartShelfStats.LastQueryMilliseconds)
        : _localization["IndexManager.SmartShelves.QueryTimeUnknown"];

    /// <summary>Localized smart-shelf index health summary.</summary>
    public string SmartShelfIndexHealthSummary => _smartShelfStats.RequiredIndexesHealthy
        ? _localization["IndexManager.SmartShelves.IndexesHealthy"]
        : string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["IndexManager.SmartShelves.IndexesMissingFormat"],
            string.Join(", ", _smartShelfStats.MissingIndexes));

    /// <summary>Localized pause OCR label.</summary>
    public string PauseOcrLabel => _localization["IndexManager.OcrJobs.Pause"];

    /// <summary>Localized cancel OCR label.</summary>
    public string CancelOcrLabel => _localization["IndexManager.OcrJobs.Cancel"];

    /// <summary>Localized retry OCR label.</summary>
    public string RetryOcrLabel => _localization["IndexManager.OcrJobs.Retry"];

    /// <summary>Localized chunk count summary.</summary>
    public string ChunkSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.ChunksFormat"],
        SearchChunkCount);

    /// <summary>Localized stale-embedding summary.</summary>
    public string StaleEmbeddingSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["IndexManager.Summary.StaleEmbeddingsFormat"],
        StaleEmbeddingCount);

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

    /// <summary>Opens embedding erasure confirmation and starts the countdown gate.</summary>
    public void RequestEmbeddingErasureConfirmation()
    {
        if (!CanEraseEmbeddings)
        {
            return;
        }

        _erasureCountdownCts?.Cancel();
        _erasureCountdownCts?.Dispose();
        _erasureCountdownCts = new CancellationTokenSource();
        _canConfirmEmbeddingErasure = false;
        OnPropertyChanged(nameof(CanConfirmEmbeddingErasure));
        IsEmbeddingErasureConfirmationOpen = true;
        _ = RunEmbeddingErasureCountdownAsync(_erasureCountdownCts.Token);
    }

    /// <summary>Dismisses embedding erasure confirmation.</summary>
    public void CancelEmbeddingErasureConfirmation()
    {
        _erasureCountdownCts?.Cancel();
        IsEmbeddingErasureConfirmationOpen = false;
        _canConfirmEmbeddingErasure = false;
        OnPropertyChanged(nameof(CanConfirmEmbeddingErasure));
    }

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

    /// <summary>Confirms and performs embedding erasure.</summary>
    public async Task ConfirmEmbeddingErasureAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirmEmbeddingErasure)
        {
            return;
        }

        _erasureCountdownCts?.Cancel();
        IsEmbeddingErasureConfirmationOpen = false;
        try
        {
            IsErasingEmbeddings = true;
            EmbeddingErasureResult result = await _embeddingErasure
                .EraseAllAsync(cancellationToken)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["IndexManager.Embeddings.ErasedFormat"],
                    result.VectorsErased,
                    result.BooksReset);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = _localization["IndexManager.Embeddings.EraseFailed"];
                ErrorItems.Add(ex.Message);
                OnPropertyChanged(nameof(HasErrors));
            });
            throw;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsErasingEmbeddings = false);
        }
    }

    /// <summary>Cancels a running rebuild.</summary>
    public void CancelRebuild() => _rebuildCts?.Cancel();

    /// <summary>Pauses a queued or running OCR job.</summary>
    public async Task PauseOcrJobAsync(OcrJobStatusDisplayItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanPause)
        {
            return;
        }

        await _indexManager.PauseOcrJobAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => StatusText = _localization["IndexManager.OcrJobs.Paused"]);
    }

    /// <summary>Cancels an OCR job.</summary>
    public async Task CancelOcrJobAsync(OcrJobStatusDisplayItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanCancel)
        {
            return;
        }

        await _indexManager.CancelOcrJobAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => StatusText = _localization["IndexManager.OcrJobs.Cancelled"]);
    }

    /// <summary>Retries a paused, cancelled, or failed OCR job.</summary>
    public async Task RetryOcrJobAsync(OcrJobStatusDisplayItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanRetry)
        {
            return;
        }

        await _indexManager.RetryOcrJobAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => StatusText = _localization["IndexManager.OcrJobs.RetryQueued"]);
    }

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
        _erasureCountdownCts?.Cancel();
        _erasureCountdownCts?.Dispose();
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
            StaleEmbeddingCount = status.StaleEmbeddingCount;
            IndexSizeBytes = status.IndexSizeBytes;
            IntegrityHealthy = status.Integrity.IsHealthy;
            _ocrJobs = status.OcrJobs;
            _smartShelfStats = status.SmartShelfStats;
            ActiveOcrJobs = status.OcrJobs.Count(job => job.State is OcrJobState.Pending or OcrJobState.Running);

            Books.Clear();
            foreach (BookIndexStatusItem book in status.Books)
            {
                Books.Add(book);
            }

            RefreshOcrJobs();

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
        OnPropertyChanged(nameof(IndexManagerIconPath));
        OnPropertyChanged(nameof(RebuildIconPath));
        OnPropertyChanged(nameof(CancelIconPath));
        OnPropertyChanged(nameof(EraseEmbeddingsIconPath));
        OnPropertyChanged(nameof(SizeIconPath));
        OnPropertyChanged(nameof(RebuildConfirmationText));
        OnPropertyChanged(nameof(ConfirmRebuildLabel));
        OnPropertyChanged(nameof(EraseEmbeddingsLabel));
        OnPropertyChanged(nameof(EmbeddingErasureConfirmationText));
        OnPropertyChanged(nameof(ConfirmEmbeddingErasureLabel));
        OnPropertyChanged(nameof(EmbeddingErasureCountdownText));
        OnPropertyChanged(nameof(IndexedSummary));
        OnPropertyChanged(nameof(FailedSummary));
        OnPropertyChanged(nameof(PendingOcrSummary));
        OnPropertyChanged(nameof(OcrJobsSummary));
        OnPropertyChanged(nameof(SmartShelfQuerySummary));
        OnPropertyChanged(nameof(SmartShelfIndexHealthSummary));
        OnPropertyChanged(nameof(PauseOcrLabel));
        OnPropertyChanged(nameof(CancelOcrLabel));
        OnPropertyChanged(nameof(RetryOcrLabel));
        OnPropertyChanged(nameof(ChunkSummary));
        OnPropertyChanged(nameof(SizeSummary));
        OnPropertyChanged(nameof(FailedPagesSummary));
        OnPropertyChanged(nameof(IntegritySummary));
        RefreshOcrJobs();
    }

    private void RaiseStatusProperties()
    {
        OnPropertyChanged(nameof(TotalBooks));
        OnPropertyChanged(nameof(IndexedBooks));
        OnPropertyChanged(nameof(FailedBooks));
        OnPropertyChanged(nameof(PendingOcrPages));
        OnPropertyChanged(nameof(FailedExtractionPages));
        OnPropertyChanged(nameof(ActiveOcrJobs));
        OnPropertyChanged(nameof(SearchChunkCount));
        OnPropertyChanged(nameof(StaleEmbeddingCount));
        OnPropertyChanged(nameof(IndexSizeBytes));
        OnPropertyChanged(nameof(IntegrityHealthy));
        OnPropertyChanged(nameof(SmartShelfIndexesHealthy));
        OnPropertyChanged(nameof(IndexedSummary));
        OnPropertyChanged(nameof(FailedSummary));
        OnPropertyChanged(nameof(PendingOcrSummary));
        OnPropertyChanged(nameof(OcrJobsSummary));
        OnPropertyChanged(nameof(ChunkSummary));
        OnPropertyChanged(nameof(StaleEmbeddingSummary));
        OnPropertyChanged(nameof(SizeSummary));
        OnPropertyChanged(nameof(FailedPagesSummary));
        OnPropertyChanged(nameof(IntegritySummary));
        OnPropertyChanged(nameof(SmartShelfQuerySummary));
        OnPropertyChanged(nameof(SmartShelfIndexHealthSummary));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasOcrJobs));
    }

    private void RefreshOcrJobs()
    {
        OcrJobs.Clear();
        foreach (OcrJobStatusItem job in _ocrJobs)
        {
            OcrJobs.Add(new OcrJobStatusDisplayItem(
                job.JobId,
                job.BookId ?? string.Empty,
                string.IsNullOrWhiteSpace(job.Title) ? job.BookId ?? _localization["IndexManager.OcrJobs.UnknownBook"] : job.Title,
                _localization[$"IndexManager.OcrJobs.State.{job.State}"],
                FormatOcrProgress(job),
                job.ErrorMessage ?? string.Empty,
                job.State is OcrJobState.Pending or OcrJobState.Running,
                job.State is OcrJobState.Pending or OcrJobState.Running or OcrJobState.Failed or OcrJobState.Paused,
                job.State is OcrJobState.Failed or OcrJobState.Cancelled or OcrJobState.Paused,
                PauseOcrLabel,
                CancelOcrLabel,
                RetryOcrLabel));
        }

        OnPropertyChanged(nameof(HasOcrJobs));
    }

    private string FormatOcrProgress(OcrJobStatusItem job) =>
        job.HasPageProgress
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["IndexManager.OcrJobs.ProgressFormat"],
                job.ProcessedPages,
                job.TotalPages,
                job.PercentComplete)
            : string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["IndexManager.OcrJobs.ProgressUnknownFormat"],
                job.ProcessedPages);

    private async Task RunEmbeddingErasureCountdownAsync(CancellationToken cancellationToken)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(_erasureConfirmationDelay.TotalSeconds));
        await Dispatcher.UIThread.InvokeAsync(() => EmbeddingErasureCountdownSeconds = seconds);

        while (seconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            seconds--;
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => EmbeddingErasureCountdownSeconds = seconds);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _canConfirmEmbeddingErasure = true;
            OnPropertyChanged(nameof(CanConfirmEmbeddingErasure));
        });
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

/// <summary>Localized OCR job row for the Index Manager UI.</summary>
public sealed record OcrJobStatusDisplayItem(
    long JobId,
    string BookId,
    string Title,
    string StateText,
    string ProgressText,
    string ErrorMessage,
    bool CanPause,
    bool CanCancel,
    bool CanRetry,
    string PauseLabel,
    string CancelLabel,
    string RetryLabel);
