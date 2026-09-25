using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Computes <see cref="ProcessingSnapshot"/> from the durable job table (Sept-23 Phase 06,
/// T06.6, K13). Refresh requests are coalesced (at most one query every 250 ms) and, while
/// processing is active, the snapshot is re-polled every two seconds so progress stays
/// truthful even when a task finishes through a path that does not notify.
/// </summary>
public sealed class ProcessingProgressService : IProcessingProgressService, IDisposable
{
    /// <summary>Job types that are records rather than work and never count as tasks.</summary>
    internal static readonly string[] RecordJobTypes = ["IngestionFailure", "ExtractionFailed"];

    private static readonly string[] AssetJobTypes = ["ThumbnailGeneration", "MetadataExtraction"];
    private static readonly TimeSpan CoalesceDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ActivePollInterval = TimeSpan.FromSeconds(2);

    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _disposed = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private ProcessingSnapshot _snapshot = ProcessingSnapshot.Idle;
    private int _terminalAtBatchStart = -1;
    private int _failedAtBatchStart;
    private int _refreshScheduled;

    /// <summary>Initializes a new instance of the <see cref="ProcessingProgressService"/> class.</summary>
    /// <param name="contextFactory">The catalogue context factory.</param>
    /// <param name="logger">Optional logger.</param>
    public ProcessingProgressService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILogger<ProcessingProgressService>? logger = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public ProcessingSnapshot CurrentSnapshot => Volatile.Read(ref _snapshot);

    /// <inheritdoc />
    public event EventHandler<ProcessingSnapshot>? ProgressChanged;

    /// <inheritdoc />
    public void NotifyChanged() => Schedule(CoalesceDelay);

    /// <inheritdoc />
    public async Task<ProcessingSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ProcessingSnapshot next;
        bool changed;
        try
        {
            JobCounts counts = await ReadCountsAsync(cancellationToken).ConfigureAwait(false);
            ProcessingSnapshot previous = CurrentSnapshot;
            bool active = counts.Queued + counts.Running > 0;
            if (active && (!previous.IsActive || _terminalAtBatchStart < 0))
            {
                // A new batch starts: count completions from here.
                _terminalAtBatchStart = counts.Terminal;
                _failedAtBatchStart = counts.Failed;
            }

            int done = active || previous.IsActive
                ? Math.Max(0, counts.Terminal - Math.Max(_terminalAtBatchStart, 0))
                : 0;
            int failed = active || previous.IsActive
                ? Math.Max(0, counts.Failed - _failedAtBatchStart)
                : 0;
            next = new ProcessingSnapshot(
                counts.Queued,
                counts.Running,
                done,
                Math.Min(failed, done),
                counts.Waiting,
                counts.WaitingByCapability,
                counts.AssetsCompleted);
            if (!active)
            {
                _terminalAtBatchStart = -1;
            }

            changed = !SameCounts(previous, next);
            Volatile.Write(ref _snapshot, next);
        }
        finally
        {
            _refreshGate.Release();
        }

        if (changed)
        {
            ProgressChanged?.Invoke(this, next);
        }

        if (next.IsActive)
        {
            Schedule(ActivePollInterval);
        }

        return next;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
        _refreshGate.Dispose();
    }

    private void Schedule(TimeSpan delay)
    {
        if (_disposed.IsCancellationRequested || Interlocked.Exchange(ref _refreshScheduled, 1) == 1)
        {
            return;
        }

        CancellationToken token = _disposed.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _refreshScheduled, 0);
                    await RefreshAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Intentionally ignored: the service is shutting down.
                }
                catch (ObjectDisposedException)
                {
                    // Intentionally ignored: the service was disposed during a refresh.
                }
                catch (Exception exception)
                {
                    Interlocked.Exchange(ref _refreshScheduled, 0);
                    InfrastructureLog.BestEffortStepFailed(
                        _logger,
                        exception,
                        nameof(ProcessingProgressService),
                        "jobs.progress.refresh");
                }
            },
            CancellationToken.None);
    }

    private async Task<JobCounts> ReadCountsAsync(CancellationToken cancellationToken)
    {
        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await context.Jobs
            .AsNoTracking()
            .Where(job => !RecordJobTypes.Contains(job.JobType))
            .GroupBy(job => new { job.Status, job.WaitingCapability })
            .Select(group => new { group.Key.Status, group.Key.WaitingCapability, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int assets = await context.Jobs
            .AsNoTracking()
            .CountAsync(
                job => AssetJobTypes.Contains(job.JobType) && job.Status == (int)JobRuntimeStatus.Completed,
                cancellationToken)
            .ConfigureAwait(false);

        int Sum(params JobRuntimeStatus[] statuses) =>
            rows.Where(row => statuses.Contains((JobRuntimeStatus)row.Status)).Sum(row => row.Count);

        Dictionary<string, int> waitingBy = rows
            .Where(row => row.Status == (int)JobRuntimeStatus.WaitingForCapability)
            .GroupBy(row => row.WaitingCapability ?? "unknown", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Count), StringComparer.Ordinal);

        return new JobCounts(
            Queued: Sum(JobRuntimeStatus.Pending),
            Running: Sum(JobRuntimeStatus.Running),
            Terminal: Sum(
                JobRuntimeStatus.Completed,
                JobRuntimeStatus.Failed,
                JobRuntimeStatus.Cancelled,
                JobRuntimeStatus.DeadLetter),
            Failed: Sum(JobRuntimeStatus.Failed, JobRuntimeStatus.DeadLetter),
            Waiting: Sum(JobRuntimeStatus.WaitingForCapability),
            WaitingByCapability: waitingBy,
            AssetsCompleted: assets);
    }

    private static bool SameCounts(ProcessingSnapshot a, ProcessingSnapshot b) =>
        a.Queued == b.Queued &&
        a.Running == b.Running &&
        a.Done == b.Done &&
        a.Failed == b.Failed &&
        a.WaitingForCapability == b.WaitingForCapability &&
        a.AssetsCompleted == b.AssetsCompleted;

    private sealed record JobCounts(
        int Queued,
        int Running,
        int Terminal,
        int Failed,
        int Waiting,
        IReadOnlyDictionary<string, int> WaitingByCapability,
        int AssetsCompleted);
}
