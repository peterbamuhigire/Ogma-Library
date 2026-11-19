using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>One library health measure shown as a dashboard tile.</summary>
public enum LibraryHealthTileKind
{
    /// <summary>Books with no author.</summary>
    MissingAuthor = 0,

    /// <summary>Books with no ISBN.</summary>
    MissingIsbn = 1,

    /// <summary>Books with no cover.</summary>
    MissingCover = 2,

    /// <summary>Books with no publication year.</summary>
    MissingYear = 3,

    /// <summary>Background jobs that failed.</summary>
    FailedJobs = 4,

    /// <summary>Books whose file is no longer available.</summary>
    Unavailable = 5,

    /// <summary>Books with several copies or editions.</summary>
    PossibleDuplicates = 6,

    /// <summary>Books whose metadata quality is low.</summary>
    LowQuality = 7,

    /// <summary>Files that were set aside instead of being added as books.</summary>
    NeedsAttention = 8,
}

/// <summary>One tile of the library health dashboard.</summary>
public sealed class LibraryHealthTileViewModel : INotifyPropertyChanged
{
    private int _count;
    private string _label = string.Empty;
    private string _countText = string.Empty;
    private string _help = string.Empty;
    private string _accessibleName = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="LibraryHealthTileViewModel"/> class.</summary>
    /// <param name="kind">The measure.</param>
    public LibraryHealthTileViewModel(LibraryHealthTileKind kind)
    {
        Kind = kind;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The measure.</summary>
    public LibraryHealthTileKind Kind { get; }

    /// <summary>The stable UI Automation id, for example <c>Health.Tile.MissingAuthor</c>.</summary>
    public string AutomationId => "Health.Tile." + Kind;

    /// <summary>Whether activating the tile applies a catalogue filter.</summary>
    public bool AppliesFilter => Kind is not (LibraryHealthTileKind.FailedJobs or LibraryHealthTileKind.NeedsAttention);

    /// <summary>The number of affected books, jobs or files.</summary>
    public int Count { get => _count; internal set => Set(ref _count, value); }

    /// <summary>Whether something needs the user's attention.</summary>
    public bool HasIssues => _count > 0;

    /// <summary>The tile title.</summary>
    public string Label { get => _label; internal set => Set(ref _label, value); }

    /// <summary>The count with its unit, for example "3 books".</summary>
    public string CountText { get => _countText; internal set => Set(ref _countText, value); }

    /// <summary>What activating the tile does.</summary>
    public string Help { get => _help; internal set => Set(ref _help, value); }

    /// <summary>The tile's accessible name, for example "Missing author: 3 books".</summary>
    public string AccessibleName { get => _accessibleName; internal set => Set(ref _accessibleName, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(Count))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasIssues)));
        }
    }
}

/// <summary>A failed background job row with its Retry action.</summary>
/// <param name="JobId">The job identifier.</param>
/// <param name="Text">The visible description.</param>
/// <param name="RetryName">The accessible name of the Retry button.</param>
public sealed record LibraryHealthFailedJobRow(long JobId, string Text, string RetryName);

/// <summary>A batch enrichment run row with pause and resume state.</summary>
/// <param name="BatchId">The batch identifier.</param>
/// <param name="Text">The visible progress description.</param>
/// <param name="CanPause">Whether pending jobs can be paused.</param>
/// <param name="CanResume">Whether paused or failed jobs can be resumed.</param>
/// <param name="PauseName">The accessible name of the Pause button.</param>
/// <param name="ResumeName">The accessible name of the Resume button.</param>
public sealed record LibraryHealthBatchRow(
    string BatchId,
    string Text,
    bool CanPause,
    bool CanResume,
    string PauseName,
    string ResumeName);

/// <summary>
/// The library health dashboard (Sept-23 Phase 10, task 10.10, META-008). Book tiles count with the
/// catalogue's own criteria (<see cref="ICatalogueReadModel.CountAsync"/>), so a tile's number equals
/// what its filter then shows; jobs and batch runs come from
/// <see cref="ILibraryHealthService.GetHealthSnapshotAsync"/>. A book tile replaces the live
/// catalogue filter and asks the shell to show the catalogue.
/// </summary>
public sealed class LibraryHealthViewModel : INotifyPropertyChanged
{
    /// <summary>The metadata quality score below which a book counts as low quality.</summary>
    public const double LowQualityThreshold = 0.5;

    private readonly ICatalogueReadModel _readModel;
    private readonly ILibraryHealthService _health;
    private readonly CatalogueFilterViewModel _filter;
    private readonly ILocalizationService _localization;
    private readonly ICapabilityState? _capabilities;
    private readonly ILibraryAttentionService? _attention;
    private readonly IUserNotifier? _notifier;
    private readonly ILogger _logger;
    private readonly Dictionary<LibraryHealthTileKind, LibraryHealthTileViewModel> _tiles = [];
    private IReadOnlyList<FailedJobEntry> _failedJobs = [];
    private IReadOnlyList<BatchEnrichmentRunEntry> _batchRuns = [];
    private DateTimeOffset? _loadedUtc;
    private bool _isLoading;
    private bool _isFailedJobsExpanded;
    private bool _hasLoadFailed;

    /// <summary>Initializes a new instance of the <see cref="LibraryHealthViewModel"/> class.</summary>
    /// <param name="readModel">The catalogue read model whose criteria the tiles count.</param>
    /// <param name="health">Failed jobs, batch runs and retry.</param>
    /// <param name="filter">The live catalogue filter a tile replaces.</param>
    /// <param name="localization">The localisation service.</param>
    /// <param name="capabilities">Whether online providers (and so batch enrichment) are enabled.</param>
    /// <param name="attention">Files that need attention; the tile is hidden when absent.</param>
    /// <param name="notifier">Optional notification centre for action results.</param>
    /// <param name="logger">Optional logger.</param>
    public LibraryHealthViewModel(
        ICatalogueReadModel readModel,
        ILibraryHealthService health,
        CatalogueFilterViewModel filter,
        ILocalizationService localization,
        ICapabilityState? capabilities = null,
        ILibraryAttentionService? attention = null,
        IUserNotifier? notifier = null,
        ILogger<LibraryHealthViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(localization);
        _readModel = readModel;
        _health = health;
        _filter = filter;
        _localization = localization;
        _capabilities = capabilities;
        _attention = attention;
        _notifier = notifier;
        _logger = logger ?? (ILogger)NullLogger.Instance;

        foreach (LibraryHealthTileKind kind in Enum.GetValues<LibraryHealthTileKind>())
        {
            if (kind == LibraryHealthTileKind.NeedsAttention && attention is null)
            {
                continue;
            }

            var tile = new LibraryHealthTileViewModel(kind);
            _tiles[kind] = tile;
            Tiles.Add(tile);
        }

        if (_capabilities is not null)
        {
            _capabilities.Changed += (_, _) => UiThread(() => OnPropertyChanged(nameof(IsBatchControlsVisible)));
        }

        _localization.CultureChanged += (_, _) => RebuildTexts();
        RebuildTexts();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after a book tile replaced the catalogue filter: the shell shows the catalogue.</summary>
    public event EventHandler? ShowCatalogueRequested;

    /// <summary>Raised by the Needs attention tile: the shell opens the needs-attention list.</summary>
    public event EventHandler? ShowNeedsAttentionRequested;

    /// <summary>The tiles in display order.</summary>
    public ObservableCollection<LibraryHealthTileViewModel> Tiles { get; } = [];

    /// <summary>The failed background jobs, each with Retry.</summary>
    public ObservableCollection<LibraryHealthFailedJobRow> FailedJobs { get; } = [];

    /// <summary>The recoverable batch enrichment runs.</summary>
    public ObservableCollection<LibraryHealthBatchRow> BatchRuns { get; } = [];

    /// <summary>Whether the counts are loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether the failed-jobs list is expanded (the Failed jobs tile opens it).</summary>
    public bool IsFailedJobsExpanded
    {
        get => _isFailedJobsExpanded;
        set
        {
            if (_isFailedJobsExpanded != value)
            {
                _isFailedJobsExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether there are failed jobs to list.</summary>
    public bool HasFailedJobs => FailedJobs.Count > 0;

    /// <summary>Whether batch pause and resume are offered: only with online providers enabled.</summary>
    public bool IsBatchControlsVisible => _capabilities?.AreMetadataProvidersEnabled == true && BatchRuns.Count > 0;

    /// <summary>Whether the last load failed.</summary>
    public bool HasLoadFailed
    {
        get => _hasLoadFailed;
        private set
        {
            if (_hasLoadFailed != value)
            {
                _hasLoadFailed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The dashboard heading.</summary>
    public string Title => _localization["Health.Title"];

    /// <summary>The dashboard introduction.</summary>
    public string Intro => _localization["Health.Intro"];

    /// <summary>The accessible name of the tile group.</summary>
    public string TilesName => _localization["Health.Tiles.Name"];

    /// <summary>Label of the Refresh button.</summary>
    public string RefreshText => _localization["Health.Refresh"];

    /// <summary>Label of the Back to library button.</summary>
    public string BackText => _localization["Health.Back"];

    /// <summary>The failed-jobs section heading.</summary>
    public string FailedJobsTitle => _localization["Health.FailedJobs.Title"];

    /// <summary>Shown when no job has failed.</summary>
    public string NoFailedJobsText => _localization["Health.FailedJobs.None"];

    /// <summary>Label of a Retry button.</summary>
    public string RetryText => _localization["Health.FailedJobs.Retry"];

    /// <summary>The batch enrichment section heading.</summary>
    public string BatchTitle => _localization["Health.Batch.Title"];

    /// <summary>Label of a Pause button.</summary>
    public string PauseText => _localization["Health.Batch.Pause"];

    /// <summary>Label of a Resume button.</summary>
    public string ResumeText => _localization["Health.Batch.Resume"];

    /// <summary>Shown when the counts could not be loaded.</summary>
    public string LoadFailedText => _localization["Health.LoadFailed"];

    /// <summary>When the counts were last loaded, for example "Updated 14:05".</summary>
    public string LoadedText => _loadedUtc is { } loaded
        ? string.Format(
            _localization.CurrentCulture,
            _localization["Health.LoadedFormat"],
            loaded.ToLocalTime().ToString("t", _localization.CurrentCulture))
        : string.Empty;

    /// <summary>Replaces the live filter with the one a tile applies.</summary>
    /// <param name="kind">The tile.</param>
    /// <param name="filter">The filter to replace.</param>
    /// <returns><see langword="true"/> when the tile has a catalogue filter.</returns>
    public static bool ApplyFilter(LibraryHealthTileKind kind, CatalogueFilterViewModel filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (kind is LibraryHealthTileKind.FailedJobs or LibraryHealthTileKind.NeedsAttention)
        {
            return false;
        }

        filter.ClearAll();
        switch (kind)
        {
            case LibraryHealthTileKind.MissingAuthor:
                filter.MissingFields = CatalogueMissingFields.Author;
                break;
            case LibraryHealthTileKind.MissingIsbn:
                filter.MissingFields = CatalogueMissingFields.Isbn;
                break;
            case LibraryHealthTileKind.MissingCover:
                filter.MissingFields = CatalogueMissingFields.Cover;
                break;
            case LibraryHealthTileKind.MissingYear:
                filter.MissingFields = CatalogueMissingFields.Year;
                break;
            case LibraryHealthTileKind.Unavailable:
                filter.AvailabilityFilter = false;
                break;
            case LibraryHealthTileKind.PossibleDuplicates:
                filter.PossibleDuplicatesOnly = true;
                break;
            case LibraryHealthTileKind.LowQuality:
                filter.QualityBelow = LowQualityThreshold;
                break;
        }

        return true;
    }

    /// <summary>
    /// Activates a tile: a book tile replaces the catalogue filter and shows the catalogue; Failed
    /// jobs opens the job list; Needs attention opens the needs-attention list.
    /// </summary>
    /// <param name="kind">The tile.</param>
    public void ActivateTile(LibraryHealthTileKind kind)
    {
        switch (kind)
        {
            case LibraryHealthTileKind.FailedJobs:
                IsFailedJobsExpanded = true;
                break;
            case LibraryHealthTileKind.NeedsAttention:
                ShowNeedsAttentionRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                if (ApplyFilter(kind, _filter))
                {
                    ShowCatalogueRequested?.Invoke(this, EventArgs.Empty);
                }

                break;
        }
    }

    /// <summary>Returns to the catalogue without changing the filter (the Back button).</summary>
    public void ReturnToCatalogue() => ShowCatalogueRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Loads every count, the failed jobs and the batch runs.</summary>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var counts = new Dictionary<LibraryHealthTileKind, int>();
            foreach (LibraryHealthTileViewModel tile in Tiles.Where(tile => tile.AppliesFilter))
            {
                var filter = new CatalogueFilterViewModel();
                ApplyFilter(tile.Kind, filter);
                counts[tile.Kind] = await _readModel.CountAsync(filter.ToCriteria(), cancellationToken).ConfigureAwait(true);
            }

            LibraryHealthSnapshot snapshot = await _health.GetHealthSnapshotAsync(cancellationToken).ConfigureAwait(true);
            if (_attention is not null)
            {
                counts[LibraryHealthTileKind.NeedsAttention] = await _attention.CountAsync(cancellationToken).ConfigureAwait(true);
            }

            counts[LibraryHealthTileKind.FailedJobs] = snapshot.FailedJobs.Count;

            // The awaits stay on the caller's (UI) context, so the bound state is set there.
            foreach ((LibraryHealthTileKind kind, int count) in counts)
            {
                if (_tiles.TryGetValue(kind, out LibraryHealthTileViewModel? tile))
                {
                    tile.Count = count;
                }
            }

            _failedJobs = snapshot.FailedJobs;
            _batchRuns = snapshot.BatchEnrichmentRuns ?? [];
            _loadedUtc = snapshot.LoadedUtc;
            HasLoadFailed = false;
            RebuildTexts();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(LibraryHealthViewModel), "health_load");
            HasLoadFailed = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Re-queues one failed job and reloads the dashboard.</summary>
    /// <param name="jobId">The failed job.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default) =>
        RunAsync("health_retry", token => _health.RetryJobAsync(jobId, token), "Health.FailedJobs.Retried", cancellationToken);

    /// <summary>Pauses a batch enrichment run.</summary>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PauseBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
        RunAsync("health_pause", token => _health.PauseBatchEnrichmentAsync(batchId, token), "Health.Batch.Paused", cancellationToken);

    /// <summary>Resumes a batch enrichment run.</summary>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task ResumeBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
        RunAsync("health_resume", token => _health.ResumeBatchEnrichmentAsync(batchId, token), "Health.Batch.Resumed", cancellationToken);

    private async Task RunAsync(
        string operation,
        Func<CancellationToken, Task> action,
        string doneKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(cancellationToken).ConfigureAwait(true);
            _notifier?.Notify(new UserNotification(doneKey, UserNotificationSeverity.Information));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(LibraryHealthViewModel), operation);
            _notifier?.Notify(new UserNotification("Health.ActionFailed", UserNotificationSeverity.Warning));
        }

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    private void RebuildTexts()
    {
        CultureInfo culture = _localization.CurrentCulture;
        foreach (LibraryHealthTileViewModel tile in Tiles)
        {
            string unit = tile.Kind switch
            {
                LibraryHealthTileKind.FailedJobs => tile.Count == 1 ? "Health.Unit.Job" : "Health.Unit.Jobs",
                LibraryHealthTileKind.NeedsAttention => tile.Count == 1 ? "Health.Unit.File" : "Health.Unit.Files",
                _ => tile.Count == 1 ? "Health.Unit.Book" : "Health.Unit.Books",
            };
            tile.Label = _localization["Health.Tile." + tile.Kind];
            tile.Help = _localization["Health.Tile." + tile.Kind + ".Help"];
            tile.CountText = string.Format(culture, _localization[unit], tile.Count);
            tile.AccessibleName = string.Format(
                culture, _localization["Health.Tile.NameFormat"], tile.Label, tile.CountText);
        }

        FailedJobs.Clear();
        foreach (FailedJobEntry job in _failedJobs)
        {
            string text = string.Format(
                culture,
                _localization["Health.FailedJobs.RowFormat"],
                job.JobType,
                job.FailureCode,
                job.FailedUtc?.ToLocalTime().ToString("g", culture) ?? string.Empty);
            FailedJobs.Add(new LibraryHealthFailedJobRow(
                job.JobId,
                text,
                string.Format(culture, _localization["Health.FailedJobs.RetryNameFormat"], text)));
        }

        BatchRuns.Clear();
        foreach (BatchEnrichmentRunEntry run in _batchRuns)
        {
            string text = string.Format(
                culture,
                _localization["Health.Batch.RowFormat"],
                run.CompletedJobs,
                run.TotalJobs,
                run.PausedJobs,
                run.FailedJobs);
            BatchRuns.Add(new LibraryHealthBatchRow(
                run.BatchId,
                text,
                CanPause: run.PendingJobs > 0,
                CanResume: run.PausedJobs > 0 || run.FailedJobs > 0,
                string.Format(culture, _localization["Health.Batch.PauseNameFormat"], text),
                string.Format(culture, _localization["Health.Batch.ResumeNameFormat"], text)));
        }

        foreach (string name in new[]
                 {
                     nameof(Title), nameof(Intro), nameof(TilesName), nameof(RefreshText), nameof(BackText),
                     nameof(FailedJobsTitle), nameof(NoFailedJobsText), nameof(RetryText), nameof(BatchTitle),
                     nameof(PauseText), nameof(ResumeText), nameof(LoadFailedText), nameof(LoadedText),
                     nameof(HasFailedJobs), nameof(IsBatchControlsVisible),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private static void UiThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
