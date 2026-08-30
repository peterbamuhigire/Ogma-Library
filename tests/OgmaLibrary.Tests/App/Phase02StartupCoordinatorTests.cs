using OgmaLibrary.App.Startup;
using OgmaLibrary.Infrastructure;

namespace OgmaLibrary.Tests.App;

/// <summary>Phase 02 regression coverage for ordered and recoverable startup.</summary>
public sealed class Phase02StartupCoordinatorTests
{
    [Fact]
    public async Task RequiredFailure_BlocksCatalogue_RedactsException_AndStopsLaterTasks()
    {
        var required = RecordingTask.Failing(
            "catalogue.migration",
            StartupTaskCriticality.Required,
            "Safe database recovery guidance.",
            new InvalidOperationException("secret database path"));
        var later = RecordingTask.Successful("workers.start", StartupTaskCriticality.Optional);
        using var coordinator = CreateCoordinator([required, later]);

        ApplicationStartupReport report = await coordinator.InitializeAsync();

        Assert.False(report.CanOpenCatalogue);
        StartupTaskReport failure = Assert.Single(report.Failures);
        Assert.Equal("Safe database recovery guidance.", failure.Message);
        Assert.DoesNotContain("secret database path", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, later.ExecuteCount);
    }

    [Fact]
    public async Task OptionalFailure_LeavesCatalogueAvailable_AndLaterTasksRun()
    {
        var required = RecordingTask.Successful(
            "catalogue.migration",
            StartupTaskCriticality.Required);
        var optional = RecordingTask.Failing(
            "jobs.recovery",
            StartupTaskCriticality.Optional,
            "Background recovery can be retried.",
            new IOException("private filename"));
        var later = RecordingTask.Successful("workers.start", StartupTaskCriticality.Optional);
        using var coordinator = CreateCoordinator([required, optional, later]);

        ApplicationStartupReport report = await coordinator.InitializeAsync();

        Assert.True(report.CanOpenCatalogue);
        Assert.True(report.IsDegraded);
        Assert.Equal("io_unavailable", Assert.Single(report.Failures).Code);
        Assert.Equal(1, later.ExecuteCount);
    }

    [Fact]
    public async Task Retry_RerunsIdempotentTasks_AndShutdownStopsInReverseOrder()
    {
        var stopOrder = new List<string>();
        var first = RecordingTask.Successful(
            "catalogue.migration",
            StartupTaskCriticality.Required,
            stopOrder);
        var second = RecordingTask.FailsOnce(
            "workers.start",
            StartupTaskCriticality.Optional,
            stopOrder);
        using var coordinator = CreateCoordinator([first, second]);

        ApplicationStartupReport firstAttempt = await coordinator.InitializeAsync();
        ApplicationStartupReport retry = await coordinator.InitializeAsync();
        await coordinator.StopAsync();

        Assert.True(firstAttempt.CanOpenCatalogue);
        Assert.True(firstAttempt.IsDegraded);
        Assert.False(retry.IsDegraded);
        Assert.Equal(2, first.ExecuteCount);
        Assert.Equal(2, second.ExecuteCount);
        Assert.Equal(["workers.start", "catalogue.migration"], stopOrder);
    }

    [Fact]
    public async Task Cancellation_Propagates_WithoutConvertingToDegradedState()
    {
        var task = new RecordingTask(
            "catalogue.migration",
            StartupTaskCriticality.Required,
            _ => Task.Delay(Timeout.InfiniteTimeSpan, _));
        using var coordinator = CreateCoordinator([task]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.InitializeAsync(cancellation.Token));
    }

    private static ApplicationStartupCoordinator CreateCoordinator(
        IReadOnlyList<IApplicationStartupTask> tasks) =>
        new(tasks, new FixedCapabilityProbe(), new StopwatchBenchmarkContext());

    private sealed class FixedCapabilityProbe : IStartupCapabilityProbe
    {
        public Task<IReadOnlyList<CapabilityHealth>> ProbeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<CapabilityHealth>>(
            [
                new CapabilityHealth(
                    "metadata.external",
                    CapabilityAvailability.Disabled,
                    "disabled_by_default",
                    "External providers are disabled."),
            ]);
        }
    }

    private sealed class RecordingTask : IApplicationStartupTask, IApplicationStoppableTask
    {
        private readonly Func<CancellationToken, Task> _execute;
        private readonly List<string>? _stopOrder;

        public RecordingTask(
            string name,
            StartupTaskCriticality criticality,
            Func<CancellationToken, Task> execute,
            string failureMessage = "Safe recovery guidance.",
            List<string>? stopOrder = null)
        {
            Name = name;
            Criticality = criticality;
            _execute = execute;
            FailureMessage = failureMessage;
            _stopOrder = stopOrder;
        }

        public string Name { get; }

        public StartupTaskCriticality Criticality { get; }

        public string FailureMessage { get; }

        public int ExecuteCount { get; private set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecuteCount++;
            await _execute(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stopOrder?.Add(Name);
            return Task.CompletedTask;
        }

        public static RecordingTask Successful(
            string name,
            StartupTaskCriticality criticality,
            List<string>? stopOrder = null) =>
            new(name, criticality, _ => Task.CompletedTask, stopOrder: stopOrder);

        public static RecordingTask Failing(
            string name,
            StartupTaskCriticality criticality,
            string safeMessage,
            Exception exception) =>
            new(name, criticality, _ => Task.FromException(exception), safeMessage);

        public static RecordingTask FailsOnce(
            string name,
            StartupTaskCriticality criticality,
            List<string> stopOrder)
        {
            int attempts = 0;
            return new RecordingTask(
                name,
                criticality,
                _ => ++attempts == 1
                    ? Task.FromException(new TimeoutException("provider endpoint"))
                    : Task.CompletedTask,
                stopOrder: stopOrder);
        }
    }
}
