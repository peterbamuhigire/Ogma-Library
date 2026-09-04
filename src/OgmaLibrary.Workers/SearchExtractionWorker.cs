using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 10 search indexing. It consumes durable
/// FTS-reindex triggers when available and retains a compatibility stage poll
/// for books created before queue-backed search jobs were introduced.
/// </summary>
public sealed class SearchExtractionWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IExtractionPipelineService _pipeline;
    private readonly IJobRuntimeService? _jobRuntime;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchExtractionWorker"/>.
    /// </summary>
    public SearchExtractionWorker(
        IExtractionPipelineService pipeline,
        IJobRuntimeService? jobRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
        _jobRuntime = jobRuntime;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
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
                        await ExecuteJobAsync(lease, stoppingToken).ConfigureAwait(false);
                        continue;
                    }
                }

                ExtractionBatchResult result = await _pipeline
                    .IndexNextBatchAsync(maxBooks: 3, stoppingToken)
                    .ConfigureAwait(false);

                if (result.BooksAttempted == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(ErrorDelay, stoppingToken).ConfigureAwait(false);
            }
        }
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
                await _jobRuntime!.FailAsync(
                        lease.JobId,
                        WorkerId,
                        new JobFailure("missing_book_id", "The search job has no book identity.", Retryable: false, DeadLetter: true),
                        cancellationToken: stoppingToken)
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
            else
            {
                await _jobRuntime!.FailAsync(
                        lease.JobId,
                        WorkerId,
                        new JobFailure("search_index_failed", "Search extraction did not complete.", Retryable: true),
                        cancellationToken: stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await _jobRuntime!.FailAsync(
                    lease.JobId,
                    WorkerId,
                    new JobFailure("search_worker_exception", "Search extraction worker failed.", Retryable: true),
                    cancellationToken: CancellationToken.None)
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
            }
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
        }
    }

    private static string WorkerId => $"search-extraction-{Environment.MachineName}-{Environment.ProcessId}";
}
