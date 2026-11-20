using System.Collections.Concurrent;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 background worker scheduling tests, extended by Sept-23 Phase 14 slice 4:
/// event-driven availability, no lease churn without a provider, pause and reader yield.
/// </summary>
public sealed class EmbeddingGenerationWorkerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EmbeddingGenerationWorker_StartAsync_PollsGenerationService()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var worker = new EmbeddingGenerationWorker(generation);

        await worker.StartAsync(CancellationToken.None);
        await generation.FirstPoll.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(generation.Calls >= 1);
        Assert.Equal(16, generation.LastMaxChunks);
    }

    [Fact]
    public async Task NoProvider_TenSimulatedMinutes_NoClaimsNoAttemptsNoFailures()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var runtime = new CountingRuntime(Lease(1));
        var capability = new FakeSemanticCapability(available: false);
        var clock = new SimulatedClock(TimeSpan.FromMinutes(10));
        var worker = new EmbeddingGenerationWorker(generation, runtime, capability: capability)
        {
            Delay = clock.DelayAsync,
        };

        await worker.StartAsync(CancellationToken.None);
        await clock.Elapsed.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(clock.Ticks >= 60, $"only {clock.Ticks} idle ticks were simulated");
        Assert.Equal(0, runtime.Claims);
        Assert.Equal(0, generation.Calls);
        Assert.Empty(runtime.Failures);
        Assert.Empty(runtime.Released);
    }

    [Fact]
    public async Task ChangedEvent_ResumesParkedJobs_WithinOneLoop()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var runtime = new CountingRuntime();
        var capability = new FakeSemanticCapability(available: false);
        var worker = new EmbeddingGenerationWorker(generation, runtime, capability: capability);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await capability.Refreshed.Task.WaitAsync(Wait);
            capability.Set(available: true);

            // The idle delay is ten seconds; only the Changed wake-up can make this in time.
            string resumed = await runtime.Resumed.Task.WaitAsync(Wait);
            Assert.Equal(JobCapabilities.SemanticEmbeddings, resumed);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Pause_StopsBatches_AndResumeWakesTheWorker()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var runtime = new CountingRuntime(Lease(3));
        var control = new SemanticIndexControl();
        control.Pause();
        var clock = new SimulatedClock(TimeSpan.FromMinutes(2));
        var worker = new EmbeddingGenerationWorker(
            generation,
            runtime,
            capability: new FakeSemanticCapability(available: true),
            control: control)
        {
            Delay = clock.DelayAsync,
        };

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await clock.Elapsed.Task.WaitAsync(Wait);
            Assert.Equal(0, generation.Calls);
            Assert.Equal(0, runtime.Claims);

            control.ResumeIndexing();
            await runtime.Completed.Task.WaitAsync(Wait);
            Assert.True(generation.Calls >= 1);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PauseMidBatch_ReleasesLeaseWithoutConsumingRetry()
    {
        var generation = new BlockingEmbeddingGenerationService();
        var runtime = new CountingRuntime(Lease(4));
        var control = new SemanticIndexControl();
        var worker = new EmbeddingGenerationWorker(
            generation,
            runtime,
            capability: new FakeSemanticCapability(available: true),
            control: control);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await generation.Started.Task.WaitAsync(Wait);
            control.Pause();
            long released = await runtime.ReleasedSignal.Task.WaitAsync(Wait);
            Assert.Equal(4, released);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Empty(runtime.Failures);
        Assert.Empty(runtime.CompletedJobs);
    }

    [Fact]
    public async Task StopMidBatch_ReleasesLeaseWithoutConsumingRetry()
    {
        var generation = new BlockingEmbeddingGenerationService();
        var runtime = new CountingRuntime(Lease(5));
        var worker = new EmbeddingGenerationWorker(
            generation,
            runtime,
            capability: new FakeSemanticCapability(available: true));

        await worker.StartAsync(CancellationToken.None);
        await generation.Started.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([5L], runtime.Released);
        Assert.Empty(runtime.Failures);
    }

    [Fact]
    public async Task ActiveReaderSession_YieldsBatches_UntilTheReaderIsIdle()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var reader = new ReaderEvents();
        var clock = new SimulatedClock(TimeSpan.FromMinutes(1));
        var worker = new EmbeddingGenerationWorker(
            generation,
            new CountingRuntime(),
            capability: new FakeSemanticCapability(available: true),
            reader: reader)
        {
            Delay = clock.DelayAsync,
            ReaderActive = () => true,
        };

        await worker.StartAsync(CancellationToken.None);
        await clock.Elapsed.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, generation.Calls);
    }

    [Fact]
    public void ReaderActivity_IsTrackedFromSessionEvents()
    {
        var reader = new ReaderEvents();
        var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var tracker = new ReaderActivityTracker(reader, () => now);

        Assert.False(tracker.IsActive);
        reader.Publish(new ReaderEvent.SessionOpened("BOOK-1"));
        Assert.True(tracker.IsActive);

        now = now.AddSeconds(ReaderActivityTracker.QuietPeriod.TotalSeconds + 1);
        Assert.False(tracker.IsActive);
        reader.Publish(new ReaderEvent.PageChanged("BOOK-1", 2, 10));
        Assert.True(tracker.IsActive);

        reader.Publish(new ReaderEvent.SessionClosed("BOOK-1"));
        Assert.False(tracker.IsActive);
        tracker.Dispose();
    }

    private static JobLease Lease(long id) =>
        new(id, "EmbeddingJob", "BOOK-" + id, "{}", 1, "w", DateTimeOffset.UtcNow.AddMinutes(5));

    private sealed class SimulatedClock(TimeSpan limit)
    {
        private TimeSpan _now;
        private int _ticks;

        public TaskCompletionSource Elapsed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Ticks => Volatile.Read(ref _ticks);

        public Task DelayAsync(TimeSpan wait, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _ticks);
            _now += wait;
            if (_now >= limit)
            {
                Elapsed.TrySetResult();

                // Real time from here on, so the test can act after the simulated window.
                return Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmbeddingGenerationService : IEmbeddingGenerationService
    {
        private int _calls;

        public TaskCompletionSource FirstPoll { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls => Volatile.Read(ref _calls);

        public int LastMaxChunks { get; private set; }

        public Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
            int maxChunks,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastMaxChunks = maxChunks;
            FirstPoll.TrySetResult();
            return Task.FromResult(new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: false));
        }
    }

    private sealed class BlockingEmbeddingGenerationService : IEmbeddingGenerationService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
            int maxChunks,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: false);
        }
    }

    private sealed class ReaderEvents : IReaderSessionReadModel, IObservable<ReaderEvent>
    {
        private readonly List<IObserver<ReaderEvent>> _observers = [];

        public IObservable<ReaderEvent> Events => this;

        public IDisposable Subscribe(IObserver<ReaderEvent> observer)
        {
            _observers.Add(observer);
            return new Unsubscriber(() => _observers.Remove(observer));
        }

        public void Publish(ReaderEvent value)
        {
            foreach (IObserver<ReaderEvent> observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        private sealed class Unsubscriber(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private sealed class CountingRuntime(params JobLease[] leases) : IJobRuntimeService
    {
        private readonly ConcurrentQueue<JobLease> _leases = new(leases);
        private int _claims;

        public int Claims => Volatile.Read(ref _claims);

        public ConcurrentBag<JobFailure> Failures { get; } = [];

        public ConcurrentBag<long> Released { get; } = [];

        public ConcurrentBag<long> CompletedJobs { get; } = [];

        public TaskCompletionSource<string> Resumed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<long> ReleasedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<JobLease?> ClaimNextAsync(
            IReadOnlyCollection<string> jobTypes,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _claims);
            return Task.FromResult(_leases.TryDequeue(out JobLease? lease) ? lease : null);
        }

        public Task<int> ResumeWaitingAsync(string capability, CancellationToken cancellationToken = default)
        {
            Resumed.TrySetResult(capability);
            return Task.FromResult(1);
        }

        public Task CompleteAsync(long jobId, string workerId, CancellationToken cancellationToken = default)
        {
            CompletedJobs.Add(jobId);
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task RenewAsync(long jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FailAsync(
            long jobId,
            string workerId,
            JobFailure failure,
            int maxAttempts = 3,
            CancellationToken cancellationToken = default)
        {
            Failures.Add(failure);
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(long jobId, string workerId, CancellationToken cancellationToken = default)
        {
            Released.Add(jobId);
            ReleasedSignal.TrySetResult(jobId);
            return Task.CompletedTask;
        }

        public Task CancelPendingAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RetryFailedAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(int recentJobLimit = 100, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
