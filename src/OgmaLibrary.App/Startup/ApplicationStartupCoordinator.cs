using OgmaLibrary.Application;

namespace OgmaLibrary.App.Startup;

/// <summary>
/// Runs ordered startup tasks, isolates optional failures and records redacted
/// timing evidence for the degraded-startup surface.
/// </summary>
public sealed class ApplicationStartupCoordinator : IApplicationStartupCoordinator, IDisposable
{
    private readonly IReadOnlyList<IApplicationStartupTask> _tasks;
    private readonly IStartupCapabilityProbe _capabilityProbe;
    private readonly IBenchmarkContext _benchmark;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>Initializes the coordinator.</summary>
    public ApplicationStartupCoordinator(
        IEnumerable<IApplicationStartupTask> tasks,
        IStartupCapabilityProbe capabilityProbe,
        IBenchmarkContext benchmark)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(capabilityProbe);
        ArgumentNullException.ThrowIfNull(benchmark);
        _tasks = tasks.ToList();
        _capabilityProbe = capabilityProbe;
        _benchmark = benchmark;
    }

    /// <inheritdoc />
    public async Task<ApplicationStartupReport> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
            var reports = new List<StartupTaskReport>(_tasks.Count);
            using IDisposable totalMeasurement = _benchmark.Measure("Startup.Total");

            foreach (IApplicationStartupTask task in _tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string operationName = $"Startup.{task.Name}";
                try
                {
                    using (_benchmark.Measure(operationName))
                    {
                        await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    }

                    reports.Add(new StartupTaskReport(
                        task.Name,
                        task.Criticality,
                        Succeeded: true,
                        Code: "ready",
                        Message: "Ready",
                        _benchmark.GetLastDuration(operationName)));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    reports.Add(new StartupTaskReport(
                        task.Name,
                        task.Criticality,
                        Succeeded: false,
                        Code: ClassifyFailure(ex),
                        Message: task.FailureMessage,
                        _benchmark.GetLastDuration(operationName)));

                    if (task.Criticality == StartupTaskCriticality.Required)
                    {
                        break;
                    }
                }
            }

            IReadOnlyList<CapabilityHealth> capabilities;
            try
            {
                capabilities = await _capabilityProbe.ProbeAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                capabilities =
                [
                    new CapabilityHealth(
                        "startup.capabilities",
                        CapabilityAvailability.Unavailable,
                        "probe_failed",
                        "Capability checks could not finish. Core library startup can still continue."),
                ];
            }

            bool canOpenCatalogue = !reports.Any(report =>
                !report.Succeeded && report.Criticality == StartupTaskCriticality.Required);
            return new ApplicationStartupReport(
                startedUtc,
                DateTimeOffset.UtcNow,
                canOpenCatalogue,
                reports,
                capabilities);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IApplicationStoppableTask task in _tasks
                .OfType<IApplicationStoppableTask>()
                .Reverse())
            {
                await task.StopAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }

    private static string ClassifyFailure(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "access_denied",
        IOException => "io_unavailable",
        TimeoutException => "timeout",
        _ => "startup_task_failed",
    };
}
