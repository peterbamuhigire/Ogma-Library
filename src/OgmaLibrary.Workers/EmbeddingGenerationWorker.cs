using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker for Phase 11 semantic embedding generation. It consumes
/// durable embedding triggers when available and retains a compatibility stage
/// poll for older catalogues. Sept-23 Phase 06 (T06.1, K25): a missing embedding provider is
/// an environment condition, so jobs are parked as <see cref="JobRuntimeStatus.WaitingForCapability"/>
/// (no attempt consumed, never "failed", never keeping processing busy).
/// Sept-23 Phase 14 (slice 4, task 14.6): availability is event-driven. While
/// <see cref="ISemanticCapabilityService.IsAvailable"/> is false the worker claims nothing, so
/// there is no lease churn; a <see cref="ISemanticCapabilityService.Changed"/> event to
/// available resumes parked jobs at once and wakes the loop. <see cref="ISemanticIndexControl"/>
/// pauses batches (cancelling the one in flight and releasing its lease without consuming a
/// retry), and batches yield while the reader is in use.
/// </summary>
public sealed class EmbeddingGenerationWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResumeCheckInterval = TimeSpan.FromMinutes(1);
    private readonly IEmbeddingGenerationService _generation;
    private readonly IJobRuntimeService? _jobRuntime;
    private readonly IProcessingProgressService? _processingProgress;
    private readonly ISemanticCapabilityService? _capability;
    private readonly ISemanticIndexControl? _control;
    private readonly ReaderActivityTracker? _reader;
    private readonly ILogger _logger;
    private readonly Lock _batchGate = new();
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _batchCancellation;
    private int _resumePending;
    private DateTimeOffset _lastResumeCheckUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of <see cref="EmbeddingGenerationWorker"/>.
    /// </summary>
    /// <param name="generation">The embedding generation service.</param>
    /// <param name="jobRuntime">The durable job runtime.</param>
    /// <param name="processingProgress">Optional processing progress publisher.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="capability">The cached semantic capability; null keeps the legacy per-batch probe.</param>
    /// <param name="control">The pause and build-now control, or null.</param>
    /// <param name="reader">Reader session events used to yield while the user reads, or null.</param>
    public EmbeddingGenerationWorker(
        IEmbeddingGenerationService generation,
        IJobRuntimeService? jobRuntime = null,
        IProcessingProgressService? processingProgress = null,
        ILogger<EmbeddingGenerationWorker>? logger = null,
        ISemanticCapabilityService? capability = null,
        ISemanticIndexControl? control = null,
        IReaderSessionReadModel? reader = null)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;
        _jobRuntime = jobRuntime;
        _processingProgress = processingProgress;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _capability = capability;
        _control = control;
        _reader = reader is null ? null : new ReaderActivityTracker(reader);
        if (_capability is not null)
        {
            _capability.Changed += OnCapabilityChanged;
        }

        if (_control is not null)
        {
            _control.Changed += OnControlChanged;
        }
    }

    /// <summary>Test seam: the idle delay (simulated time in tests).</summary>
    internal Func<TimeSpan, CancellationToken, Task>? Delay { get; init; }

    /// <summary>Test seam: overrides the reader-activity signal.</summary>
    internal Func<bool>? ReaderActive { get; init; }

    private bool CapabilityAvailable => _capability?.IsAvailable ?? true;

    private bool IsReaderActive => ReaderActive?.Invoke() ?? _reader?.IsActive ?? false;

    private static string WorkerId => $"embedding-generation-{Environment.MachineName}-{Environment.ProcessId}";

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_capability is not null)
        {
            _capability.Changed -= OnCapabilityChanged;
        }

        if (_control is not null)
        {
            _control.Changed -= OnControlChanged;
        }

        _reader?.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_capability is not null)
        {
            // The one start-up probe; after this only Changed events update availability.
            try
            {
                await _capability.RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
            {
                InfrastructureLog.DegradedStep(_logger, exception, nameof(EmbeddingGenerationWorker), "semantic.capability.startup");
            }
        }

        await ResilientJobLoop.RunAsync(
                nameof(EmbeddingGenerationWorker),
                RunOnceAsync,
                IdleDelay,
                _logger,
                stoppingToken,
                WakeableDelayAsync)
            .ConfigureAwait(false);
    }

    private async Task WakeableDelayAsync(TimeSpan wait, CancellationToken stoppingToken)
    {
        TaskCompletionSource wake = Volatile.Read(ref _wake);
        if (!wake.Task.IsCompleted)
        {
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task delay = (Delay ?? Task.Delay)(wait, delayCancellation.Token);
            await Task.WhenAny(delay, wake.Task).ConfigureAwait(false);
            await delayCancellation.CancelAsync().ConfigureAwait(false);
        }

        if (wake.Task.IsCompleted)
        {
            Interlocked.CompareExchange(
                ref _wake,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                wake);
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    private void Wake() => Volatile.Read(ref _wake).TrySetResult();

    private void OnCapabilityChanged(object? sender, EventArgs e)
    {
        if (CapabilityAvailable)
        {
            Interlocked.Exchange(ref _resumePending, 1);
        }

        Wake();
    }

    private void OnControlChanged(object? sender, EventArgs e)
    {
        if (_control!.IsPaused)
        {
            lock (_batchGate)
            {
                _batchCancellation?.Cancel();
            }

            return;
        }

        Interlocked.Exchange(ref _resumePending, 1);
        Wake();
    }

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        if (Interlocked.Exchange(ref _resumePending, 0) == 1 && _jobRuntime is not null && CapabilityAvailable)
        {
            // CapabilityAvailable: give parked jobs their turn at once (task 14.6).
            _lastResumeCheckUtc = DateTimeOffset.UtcNow;
            int resumed = await _jobRuntime
                .ResumeWaitingAsync(JobCapabilities.SemanticEmbeddings, stoppingToken)
                .ConfigureAwait(false);
            if (resumed > 0)
            {
                _processingProgress?.NotifyChanged();
            }
        }

        if (_control?.IsPaused == true || !CapabilityAvailable || IsReaderActive)
        {
            // Nothing is claimed: no lease churn, no attempt consumed, no failure recorded.
            return false;
        }

        using CancellationTokenSource batch = BeginBatch(stoppingToken);
        try
        {
            return await RunBatchAsync(batch.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (batch.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            // Paused mid-batch: any lease was released; wait for the next wake-up.
            return false;
        }
        finally
        {
            lock (_batchGate)
            {
                _batchCancellation = null;
            }
        }
    }

    private CancellationTokenSource BeginBatch(CancellationToken stoppingToken)
    {
        var batch = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        lock (_batchGate)
        {
            _batchCancellation = batch;
            if (_control?.IsPaused == true)
            {
                batch.Cancel();
            }
        }

        return batch;
    }

    private async Task<bool> RunBatchAsync(CancellationToken batchToken)
    {
        if (_jobRuntime is not null)
        {
            JobLease? lease = await _jobRuntime.ClaimNextAsync(
                    ["EmbeddingJob", "EmbeddingGeneration"],
                    WorkerId,
                    LeaseDuration,
                    batchToken)
                .ConfigureAwait(false);
            if (lease is not null)
            {
                _processingProgress?.NotifyChanged();
                await ExecuteJobAsync(lease, batchToken).ConfigureAwait(false);
                return true;
            }
        }

        EmbeddingGenerationBatchResult result = await _generation
            .GenerateNextBatchAsync(maxChunks: 16, batchToken)
            .ConfigureAwait(false);
        if (!result.ProviderUnavailable && _jobRuntime is not null &&
            DateTimeOffset.UtcNow - _lastResumeCheckUtc >= ResumeCheckInterval)
        {
            // Safety net for a park that raced a Changed event (CapabilityAvailable).
            _lastResumeCheckUtc = DateTimeOffset.UtcNow;
            int resumed = await _jobRuntime
                .ResumeWaitingAsync(JobCapabilities.SemanticEmbeddings, batchToken)
                .ConfigureAwait(false);
            if (resumed > 0)
            {
                _processingProgress?.NotifyChanged();
                return true;
            }
        }

        return result.ChunksAttempted > 0 && !result.ProviderUnavailable;
    }

    private async Task ExecuteJobAsync(JobLease lease, CancellationToken batchToken)
    {
        using CancellationTokenSource heartbeatCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(batchToken);
        Task heartbeat = RenewLeaseAsync(lease.JobId, heartbeatCancellation.Token);

        try
        {
            EmbeddingGenerationBatchResult result = await _generation
                .GenerateNextBatchAsync(maxChunks: 16, batchToken)
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
                await _jobRuntime!.CompleteAsync(lease.JobId, WorkerId, batchToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (batchToken.IsCancellationRequested)
        {
            // Stopped or paused mid-batch: hand the job back without consuming a retry.
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
}
