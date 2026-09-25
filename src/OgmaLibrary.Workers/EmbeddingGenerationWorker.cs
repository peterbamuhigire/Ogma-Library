using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 11 semantic embedding generation. It consumes
/// durable embedding triggers when available and retains a compatibility stage
/// poll for older catalogues. Sept-23 Phase 06 (T06.1, K25): a missing embedding provider is
/// an environment condition, so jobs are parked as <see cref="JobRuntimeStatus.WaitingForCapability"/>
/// (no attempt consumed, never "failed", never keeping processing busy) and are resumed when
/// the provider becomes available (checked at most once a minute).
/// </summary>
public sealed class EmbeddingGenerationWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResumeCheckInterval = TimeSpan.FromMinutes(1);
    private readonly IEmbeddingGenerationService _generation;
    private readonly IJobRuntimeService? _jobRuntime;
    private readonly IProcessingProgressService? _processingProgress;
    private readonly ILogger _logger;
    private DateTimeOffset _lastResumeCheckUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationWorker"/>.
    /// </summary>
    /// <param name="generation">The embedding generation service.</param>
    /// <param name="jobRuntime">The durable job runtime.</param>
    /// <param name="processingProgress">Optional processing progress publisher.</param>
    /// <param name="logger">Optional logger.</param>
    public EmbeddingGenerationWorker(
        IEmbeddingGenerationService generation,
        IJobRuntimeService? jobRuntime = null,
        IProcessingProgressService? processingProgress = null,
        ILogger<EmbeddingGenerationWorker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;
        _jobRuntime = jobRuntime;
        _processingProgress = processingProgress;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ResilientJobLoop.RunAsync(nameof(EmbeddingGenerationWorker), RunOnceAsync, IdleDelay, _logger, stoppingToken);

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
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
                _processingProgress?.NotifyChanged();
                await ExecuteJobAsync(lease, stoppingToken).ConfigureAwait(false);
                return true;
            }
        }

        EmbeddingGenerationBatchResult result = await _generation
            .GenerateNextBatchAsync(maxChunks: 16, stoppingToken)
            .ConfigureAwait(false);
        if (!result.ProviderUnavailable && _jobRuntime is not null &&
            DateTimeOffset.UtcNow - _lastResumeCheckUtc >= ResumeCheckInterval)
        {
            // The provider is healthy: give parked jobs their turn (CapabilityAvailable).
            _lastResumeCheckUtc = DateTimeOffset.UtcNow;
            int resumed = await _jobRuntime
                .ResumeWaitingAsync(JobCapabilities.SemanticEmbeddings, stoppingToken)
                .ConfigureAwait(false);
            if (resumed > 0)
            {
                _processingProgress?.NotifyChanged();
                return true;
            }
        }

        return result.ChunksAttempted > 0 && !result.ProviderUnavailable;
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
                await FailSafelyAsync(
                        lease,
                        JobFailure.CapabilityMissing(
                            "embedding_provider_unavailable",
                            JobCapabilities.SemanticEmbeddings,
                            "Waiting for semantic search to be set up."))
                    .ConfigureAwait(false);
            }
            else if (result.ChunksFailed > 0)
            {
                await FailSafelyAsync(
                        lease,
                        new JobFailure("embedding_batch_failed", "One or more embedding chunks failed.", Retryable: true))
                    .ConfigureAwait(false);
            }
            else
            {
                await _jobRuntime!.CompleteAsync(lease.JobId, WorkerId, stoppingToken)
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
            InfrastructureLog.DegradedStep(_logger, exception, nameof(EmbeddingGenerationWorker), "jobs.execute");
            await FailSafelyAsync(
                    lease,
                    new JobFailure("embedding_worker_exception", "Embedding worker failed.", Retryable: true))
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
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(EmbeddingGenerationWorker), "jobs.fail");
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
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(EmbeddingGenerationWorker), "jobs.release");
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

    private static string WorkerId => $"embedding-generation-{Environment.MachineName}-{Environment.ProcessId}";
}
