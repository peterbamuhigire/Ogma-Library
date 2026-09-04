using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 11 semantic embedding generation. It consumes
/// durable embedding triggers when available and retains a compatibility stage
/// poll for older catalogues.
/// </summary>
public sealed class EmbeddingGenerationWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IEmbeddingGenerationService _generation;
    private readonly IJobRuntimeService? _jobRuntime;

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationWorker"/>.
    /// </summary>
    public EmbeddingGenerationWorker(
        IEmbeddingGenerationService generation,
        IJobRuntimeService? jobRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;
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
                            ["EmbeddingJob", "EmbeddingGeneration"],
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

                EmbeddingGenerationBatchResult result = await _generation
                    .GenerateNextBatchAsync(maxChunks: 16, stoppingToken)
                    .ConfigureAwait(false);

                if (result.ChunksAttempted == 0 || result.ProviderUnavailable)
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
            EmbeddingGenerationBatchResult result = await _generation
                .GenerateNextBatchAsync(maxChunks: 16, stoppingToken)
                .ConfigureAwait(false);
            if (result.ProviderUnavailable)
            {
                await _jobRuntime!.FailAsync(
                        lease.JobId,
                        WorkerId,
                        new JobFailure("embedding_provider_unavailable", "The local embedding provider is unavailable.", Retryable: true),
                        cancellationToken: stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (result.ChunksFailed > 0)
            {
                await _jobRuntime!.FailAsync(
                        lease.JobId,
                        WorkerId,
                        new JobFailure("embedding_batch_failed", "One or more embedding chunks failed.", Retryable: true),
                        cancellationToken: stoppingToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _jobRuntime!.CompleteAsync(lease.JobId, WorkerId, stoppingToken)
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
                    new JobFailure("embedding_worker_exception", "Embedding worker failed.", Retryable: true),
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

    private static string WorkerId => $"embedding-generation-{Environment.MachineName}-{Environment.ProcessId}";
}
