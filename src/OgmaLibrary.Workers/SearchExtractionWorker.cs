using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 10 search indexing. It consumes durable
/// FTS-reindex triggers when available and retains a compatibility stage poll
/// for books created before queue-backed search jobs were introduced.
/// Sept-23 Phase 06: guarded loop (T06.5), shutdown release (T06.2), permanent failures
/// (locked PDF, removed book) are not retried (T06.1).
/// </summary>
public sealed class SearchExtractionWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IExtractionPipelineService _pipeline;
    private readonly IJobRuntimeService? _jobRuntime;
    private readonly IProcessingProgressService? _processingProgress;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchExtractionWorker"/>.
    /// </summary>
    /// <param name="pipeline">The extraction pipeline.</param>
    /// <param name="jobRuntime">The durable job runtime.</param>
    /// <param name="processingProgress">Optional processing progress publisher.</param>
    /// <param name="logger">Optional logger.</param>
    public SearchExtractionWorker(
        IExtractionPipelineService pipeline,
        IJobRuntimeService? jobRuntime = null,
        IProcessingProgressService? processingProgress = null,
        ILogger<SearchExtractionWorker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
        _jobRuntime = jobRuntime;
        _processingProgress = processingProgress;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ResilientJobLoop.RunAsync(nameof(SearchExtractionWorker), RunOnceAsync, IdleDelay, _logger, stoppingToken);

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        if (_jobRuntime is not null)
        {
            JobLease? lease = await _jobRuntime.ClaimNextAsync(
                    ["FtsReindexJob", "SearchExtraction"],
                    WorkerId,
                    LeaseDuration,
                    stoppingToken)
                .ConfigureAwait(false);
            if (lease is not null)
            {
                _processingProgress?.NotifyChanged();
                await ExecuteJobAsync(lease, stoppingToken).ConfigureAwait(false);
                return true;
            }
        }

        ExtractionBatchResult result = await _pipeline
            .IndexNextBatchAsync(maxBooks: 3, stoppingToken)
            .ConfigureAwait(false);
        return result.BooksAttempted > 0;
    }

    private async Task ExecuteJobAsync(JobLease lease, CancellationToken stoppingToken)
    {
        using CancellationTokenSource heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task heartbeat = RenewLeaseAsync(lease.JobId, heartbeatCancellation.Token);

        try
        {
            if (string.IsNullOrWhiteSpace(lease.BookId))
            {
                await FailSafelyAsync(
                        lease,
                        new JobFailure("missing_book_id", "The search job has no book identity.", Retryable: false, DeadLetter: true))
                    .ConfigureAwait(false);
                return;
            }

            ExtractionBookResult result = await _pipeline
                .IndexBookAsync(lease.BookId, stoppingToken)
                .ConfigureAwait(false);
            if (result.Succeeded)
            {
                await _jobRuntime!.CompleteAsync(lease.JobId, WorkerId, stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (result.IsPermanent)
            {
                await FailSafelyAsync(
                        lease,
                        JobFailure.Cancelled("search_index_not_possible", "The book cannot be indexed in its current state."))
                    .ConfigureAwait(false);
            }
            else
            {
                await FailSafelyAsync(
                        lease,
                        new JobFailure("search_index_failed", "Search extraction did not complete.", Retryable: true))
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await ReleaseSafelyAsync(lease).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            // Includes HttpClient timeouts (TaskCanceledException without shutdown): retryable.
            InfrastructureLog.DegradedStep(_logger, exception, nameof(SearchExtractionWorker), "jobs.execute");
            await FailSafelyAsync(
                    lease,
                    new JobFailure("search_worker_exception", "Search extraction worker failed.", Retryable: true))
                .ConfigureAwait(false);
        }
        finally
        {
            await heartbeatCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await heartbeat.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (heartbeatCancellation.IsCancellationRequested)
            {
                // Intentionally ignored: the heartbeat ends with its job.
            }

            _processingProgress?.NotifyChanged();
        }
    }

    private async Task FailSafelyAsync(JobLease lease, JobFailure failure)
    {
        try
        {
            await _jobRuntime!.FailAsync(lease.JobId, WorkerId, failure, cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(SearchExtractionWorker), "jobs.fail");
        }
    }

    private async Task ReleaseSafelyAsync(JobLease lease)
    {
        try
        {
            await _jobRuntime!.ReleaseAsync(lease.JobId, WorkerId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(SearchExtractionWorker), "jobs.release");
        }
    }

    private async Task RenewLeaseAsync(long jobId, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(LeaseDuration / 2);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await _jobRuntime!.RenewAsync(jobId, WorkerId, LeaseDuration, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Intentionally ignored: the heartbeat ends with its job.
        }
    }

    private static string WorkerId => $"search-extraction-{Environment.MachineName}-{Environment.ProcessId}";
}
