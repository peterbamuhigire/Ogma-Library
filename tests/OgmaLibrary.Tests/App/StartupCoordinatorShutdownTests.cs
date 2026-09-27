using System.Diagnostics;
using OgmaLibrary.App.Startup;
using OgmaLibrary.Infrastructure;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 K71 (LaunchCycles): closing the window while startup work (migration, backfill) is
/// still running must not wait for that work when nothing stoppable has started yet.
/// </summary>
public sealed class StartupCoordinatorShutdownTests
{
    [Fact]
    public async Task StopAsync_DuringANonStoppableStartupTask_ReturnsWithoutWaitingForIt()
    {
        var migration = new BlockingTask();
        var workers = new StoppableTask();
        using var coordinator = new ApplicationStartupCoordinator(
            [migration, workers],
            new EmptyProbe(),
            new StopwatchBenchmarkContext());
        using var lifetime = new CancellationTokenSource();

        Task<ApplicationStartupReport> startup = Task.Run(() => coordinator.InitializeAsync(lifetime.Token));
        await migration.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        lifetime.Cancel();
        var watch = Stopwatch.StartNew();
        await coordinator.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"StopAsync waited {watch.ElapsedMilliseconds} ms for the migration.");
        migration.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup);
        Assert.Equal(0, workers.Started);
    }

    [Fact]
    public async Task StopAsync_AfterStartupCompleted_StopsStartedServices()
    {
        var workers = new StoppableTask();
        using var coordinator = new ApplicationStartupCoordinator(
            [workers],
            new EmptyProbe(),
            new StopwatchBenchmarkContext());

        await coordinator.InitializeAsync();
        await coordinator.StopAsync();

        Assert.Equal(1, workers.Started);
        Assert.Equal(1, workers.Stopped);
    }

    private sealed class BlockingTask : IApplicationStartupTask
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "catalogue.migration";

        public StartupTaskCriticality Criticality => StartupTaskCriticality.Required;

        public string FailureMessage => "failed";

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Entered.TrySetResult();

            // Like a SQLite migration step: does not observe cancellation while it runs.
            await Release.Task.ConfigureAwait(false);
        }
    }

    private sealed class StoppableTask : IApplicationStartupTask, IApplicationStoppableTask
    {
        public int Started { get; private set; }

        public int Stopped { get; private set; }

        public string Name => "workers.start";

        public StartupTaskCriticality Criticality => StartupTaskCriticality.Optional;

        public string FailureMessage => "failed";

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Started++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped++;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyProbe : IStartupCapabilityProbe
    {
        public Task<IReadOnlyList<CapabilityHealth>> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityHealth>>([]);
    }
}
