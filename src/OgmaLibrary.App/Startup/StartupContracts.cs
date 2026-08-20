namespace OgmaLibrary.App.Startup;

/// <summary>Whether a startup task is required to open the catalogue.</summary>
public enum StartupTaskCriticality
{
    /// <summary>The catalogue cannot safely open when this task fails.</summary>
    Required = 0,

    /// <summary>The catalogue can open with the affected capability unavailable.</summary>
    Optional = 1,
}

/// <summary>A deterministic application startup task.</summary>
public interface IApplicationStartupTask
{
    /// <summary>Stable task identifier used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Failure impact on catalogue availability.</summary>
    StartupTaskCriticality Criticality { get; }

    /// <summary>Safe, localized-ready failure copy with no exception details.</summary>
    string FailureMessage { get; }

    /// <summary>Executes the idempotent startup operation.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>Optional shutdown behavior for a startup task.</summary>
public interface IApplicationStoppableTask
{
    /// <summary>Stops resources that the task successfully started.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>Availability state of a runtime capability.</summary>
public enum CapabilityAvailability
{
    /// <summary>The capability is configured and its local prerequisite is present.</summary>
    Available = 0,

    /// <summary>The capability is intentionally disabled.</summary>
    Disabled = 1,

    /// <summary>The capability cannot currently run but does not block the catalogue.</summary>
    Unavailable = 2,

    /// <summary>Final runtime detection is deferred until the capability is opened.</summary>
    DetectionPending = 3,
}

/// <summary>Safe capability status for UI and diagnostic export.</summary>
public sealed record CapabilityHealth(
    string Name,
    CapabilityAvailability Availability,
    string Code,
    string Summary);

/// <summary>Per-task startup evidence.</summary>
public sealed record StartupTaskReport(
    string Name,
    StartupTaskCriticality Criticality,
    bool Succeeded,
    string Code,
    string Message,
    TimeSpan Duration);

/// <summary>Aggregate result of one startup or retry attempt.</summary>
public sealed record ApplicationStartupReport(
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    bool CanOpenCatalogue,
    IReadOnlyList<StartupTaskReport> Tasks,
    IReadOnlyList<CapabilityHealth> Capabilities)
{
    /// <summary>Whether any startup task failed.</summary>
    public bool IsDegraded => Tasks.Any(task => !task.Succeeded);

    /// <summary>Safe failure records shown in the recovery surface.</summary>
    public IReadOnlyList<StartupTaskReport> Failures =>
        Tasks.Where(task => !task.Succeeded).ToList();
}

/// <summary>Non-invasive local capability probes run during startup.</summary>
public interface IStartupCapabilityProbe
{
    /// <summary>Returns capability health without making external network calls.</summary>
    Task<IReadOnlyList<CapabilityHealth>> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>Coordinates recoverable application startup and shutdown.</summary>
public interface IApplicationStartupCoordinator
{
    /// <summary>Runs or retries startup tasks in their registered order.</summary>
    Task<ApplicationStartupReport> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops successfully started services in reverse order.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
