using Microsoft.Extensions.Logging;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>
/// Source-generated log events shared by Infrastructure and Workers services (Sept-23 Phase 02,
/// T02.4). Messages name the component and step only; file names, titles and payloads are never
/// passed, and the file sink redacts anything that slips through.
/// </summary>
public static partial class InfrastructureLog
{
    /// <summary>A best-effort step failed and was intentionally ignored.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The ignored failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5001, EventName = "infrastructure.step.ignored", Level = LogLevel.Debug,
        Message = "{Component} best-effort step {Step} failed and was ignored")]
    public static partial void BestEffortStepFailed(ILogger logger, Exception exception, string component, string step);

    /// <summary>An operation failed and the service degraded to a safe fallback.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5002, EventName = "infrastructure.step.degraded", Level = LogLevel.Warning,
        Message = "{Component} step {Step} failed; continuing with a safe fallback")]
    public static partial void DegradedStep(ILogger logger, Exception exception, string component, string step);

    /// <summary>A recovery or audit step failed after another failure.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5003, EventName = "infrastructure.recovery.failed", Level = LogLevel.Error,
        Message = "{Component} recovery step {Step} failed")]
    public static partial void RecoveryStepFailed(ILogger logger, Exception exception, string component, string step);

    /// <summary>A background job loop iteration failed and will retry.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="worker">The worker name.</param>
    [LoggerMessage(EventId = 5004, EventName = "job.retry.scheduled", Level = LogLevel.Warning,
        Message = "{Worker} iteration failed; retrying after a delay")]
    public static partial void WorkerRetryScheduled(ILogger logger, Exception exception, string worker);
}
