using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Ocr;

namespace OgmaLibrary.App.ViewModels.Search;

/// <summary>Privacy-safe operator surface for durable background work.</summary>
public sealed class ActivityCentreViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IJobRuntimeService _runtime;
    private readonly ILocalizationService _localization;
    private readonly IOcrJobQueueService? _ocrJobs;
    private IReadOnlyList<JobRuntimeDiagnostic> _diagnostics = [];
    private int _booksNeedingOcr;
    private bool _isBusy;
    private string _statusText;

    /// <summary>Initializes a new instance of the <see cref="ActivityCentreViewModel"/> class.</summary>
    /// <param name="runtime">The job runtime.</param>
    /// <param name="localization">The localisation service.</param>
    /// <param name="ocrJobs">Optional OCR queue for the "make scanned books searchable" banner.</param>
    public ActivityCentreViewModel(
        IJobRuntimeService runtime,
        ILocalizationService localization,
        IOcrJobQueueService? ocrJobs = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(localization);
        _runtime = runtime;
        _localization = localization;
        _ocrJobs = ocrJobs;
        _statusText = _localization["ActivityCentre.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Recent jobs, newest first, with no payload or free-form error text.</summary>
    public ObservableCollection<ActivityJobDisplayItem> Jobs { get; } = [];

    /// <summary>True while a load or operator action is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAct));
            }
        }
    }

    /// <summary>Localized status suitable for an accessibility announcement.</summary>
    public string StatusText
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

    /// <summary>Whether operator controls are available.</summary>
    public bool CanAct => !IsBusy;

    /// <summary>Whether recent jobs are present.</summary>
    public bool HasJobs => Jobs.Count > 0;

    /// <summary>Localized section title.</summary>
    public string Title => _localization["ActivityCentre.Title"];

    /// <summary>Localized refresh label.</summary>
    public string RefreshLabel => _localization["ActivityCentre.Refresh"];

    /// <summary>Localized export label.</summary>
    public string ExportLabel => _localization["ActivityCentre.Export"];

    /// <summary>Localized retry label.</summary>
    public string RetryLabel => _localization["ActivityCentre.Retry"];

    /// <summary>Localized cancel label.</summary>
    public string CancelLabel => _localization["ActivityCentre.Cancel"];

    /// <summary>Localized "Retry all" label (Sept-23 Phase 06, T06.11).</summary>
    public string RetryAllLabel => _localization["ActivityCentre.RetryAll"];

    /// <summary>Whether terminal failed jobs exist that "Retry all" can queue again.</summary>
    public bool HasFailedJobs { get; private set; }

    /// <summary>Localized queue totals.</summary>
    public string QueueSummary { get; private set; } = string.Empty;

    /// <summary>Localized failure totals.</summary>
    public string FailureSummary { get; private set; } = string.Empty;

    /// <summary>Books whose text status says OCR would make them searchable (Sept-23 Phase 17).</summary>
    public int BooksNeedingOcr => _booksNeedingOcr;

    /// <summary>Whether the "make scanned books searchable" banner is shown.</summary>
    public bool HasOcrSuggestion => _ocrJobs is not null && _booksNeedingOcr > 0;

    /// <summary>Localized banner text, for example "2 scanned book(s) cannot be searched yet."</summary>
    public string OcrSuggestionText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["ActivityCentre.Ocr.SuggestionFormat"],
        _booksNeedingOcr);

    /// <summary>Localized banner action, for example "Make 2 scanned book(s) searchable".</summary>
    public string MakeSearchableLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["ActivityCentre.Ocr.MakeSearchableFormat"],
        _booksNeedingOcr);

    /// <summary>
    /// Queues OCR for every book that needs it (banner action; Sept-23 Phase 17, task 3), then
    /// refreshes the snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of books queued.</returns>
    public async Task<int> MakeScannedBooksSearchableAsync(CancellationToken cancellationToken = default)
    {
        if (_ocrJobs is null || IsBusy)
        {
            return 0;
        }

        int queued = await _ocrJobs.QueueBooksNeedingOcrAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["ActivityCentre.Ocr.QueuedFormat"],
                queued));
        return queued;
    }

    /// <summary>Loads a bounded operational snapshot.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsBusy = true);
            JobRuntimeDiagnostics snapshot = await _runtime
                .GetDiagnosticsAsync(100, cancellationToken)
                .ConfigureAwait(false);
            int needingOcr = _ocrJobs is null
                ? 0
                : await _ocrJobs.CountBooksNeedingOcrAsync(cancellationToken).ConfigureAwait(false);
            _diagnostics = snapshot.RecentJobs;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _booksNeedingOcr = needingOcr;
                ApplySnapshot(snapshot);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusText = _localization["ActivityCentre.Status.Failed"]);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
        }
    }

    /// <summary>Retries one terminal failed job, then refreshes the snapshot.</summary>
    public async Task RetryAsync(ActivityJobDisplayItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanRetry || IsBusy)
        {
            return;
        }

        await _runtime.RetryFailedAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = _localization["ActivityCentre.Status.RetryQueued"]);
    }

    /// <summary>Queues every terminal failed job in the snapshot again (T06.11).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the jobs are queued and the snapshot refreshed.</returns>
    public async Task RetryAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        foreach (JobRuntimeDiagnostic job in _diagnostics.Where(job => job.Status == JobRuntimeStatus.Failed).ToArray())
        {
            await _runtime.RetryFailedAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        }

        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = _localization["ActivityCentre.Status.RetryAllQueued"]);
    }

    /// <summary>Cancels one pending job, then refreshes the snapshot.</summary>
    public async Task CancelAsync(ActivityJobDisplayItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanCancel || IsBusy)
        {
            return;
        }

        await _runtime.CancelPendingAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = _localization["ActivityCentre.Status.Cancelled"]);
    }

    /// <summary>Writes the redacted runtime snapshot to a caller-selected stream.</summary>
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        string json = await _runtime.ExportDiagnosticsJsonAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        await destination.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = _localization["ActivityCentre.Status.Exported"]);
    }

    /// <inheritdoc />
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private void ApplySnapshot(JobRuntimeDiagnostics snapshot)
    {
        Jobs.Clear();
        foreach (JobRuntimeDiagnostic job in snapshot.RecentJobs)
        {
            Jobs.Add(ToDisplayItem(job));
        }

        JobRuntimeMetrics metrics = snapshot.Metrics;
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.CurrentCulture;
        QueueSummary = string.Format(
            culture,
            _localization["ActivityCentre.Queue.ActiveFormat"],
            metrics.PendingCount,
            metrics.RunningCount);

        // Sept-23 Phase 06 (T06.11, K25): waiting-for-setup, needs-attention and failed are
        // distinct; raw attempt totals are never a headline.
        var parts = new List<string>();
        if (metrics.WaitingForCapabilityCount > 0)
        {
            parts.Add(string.Format(
                culture,
                _localization["ActivityCentre.Waiting.SemanticFormat"],
                metrics.WaitingForCapabilityCount));
        }

        if (metrics.DeadLetterCount > 0)
        {
            parts.Add(string.Format(culture, _localization["ActivityCentre.NeedsAttentionFormat"], metrics.DeadLetterCount));
        }

        parts.Add(string.Format(culture, _localization["ActivityCentre.FailedFormat"], metrics.FailedCount));
        FailureSummary = string.Join(" · ", parts);
        HasFailedJobs = metrics.FailedCount > 0;
        StatusText = _localization["ActivityCentre.Status.Loaded"];
        OnPropertyChanged(nameof(HasFailedJobs));
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(FailureSummary));
        RaiseOcrSuggestionChanged();
    }

    private void RaiseOcrSuggestionChanged()
    {
        OnPropertyChanged(nameof(BooksNeedingOcr));
        OnPropertyChanged(nameof(HasOcrSuggestion));
        OnPropertyChanged(nameof(OcrSuggestionText));
        OnPropertyChanged(nameof(MakeSearchableLabel));
    }

    private ActivityJobDisplayItem ToDisplayItem(JobRuntimeDiagnostic job)
    {
        string state = _localization[$"ActivityCentre.State.{job.Status}"];

        // Sept-23 Phase 17 (task 4): an OCR failure shows its localised reason, never a raw code.
        bool ocrFailure = OcrFailureCodes.IsOcrCode(job.FailureCode) &&
            job.Status is JobRuntimeStatus.Failed or JobRuntimeStatus.DeadLetter or JobRuntimeStatus.Pending;
        string? failureText = ocrFailure
            ? _localization[OcrFailureCodes.LocalizationKey(job.FailureCode)]
            : null;
        return new ActivityJobDisplayItem(
            job.JobId,
            job.JobType,
            failureText is null
                ? state
                : string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["ActivityCentre.FailureReasonFormat"],
                    state,
                    failureText),
            job.Attempt,
            job.Status == JobRuntimeStatus.Failed,
            job.Status == JobRuntimeStatus.Pending,
            RetryLabel,
            CancelLabel,
            failureText);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        JobRuntimeDiagnostic[] diagnostics = _diagnostics.ToArray();
        Jobs.Clear();
        foreach (JobRuntimeDiagnostic job in diagnostics)
        {
            Jobs.Add(ToDisplayItem(job));
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(ExportLabel));
        OnPropertyChanged(nameof(RetryLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(RetryAllLabel));
        OnPropertyChanged(nameof(HasJobs));
        RaiseOcrSuggestionChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>UI-safe background job row.</summary>
public sealed record ActivityJobDisplayItem(
    long JobId,
    string JobType,
    string StateText,
    int Attempt,
    bool CanRetry,
    bool CanCancel,
    string RetryLabel,
    string CancelLabel,
    string? FailureText = null);
