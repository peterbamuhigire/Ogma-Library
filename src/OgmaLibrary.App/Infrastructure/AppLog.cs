using Microsoft.Extensions.Logging;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// Source-generated log events for the desktop App (Sept-23 Phase 02, T02.3). Event names follow
/// <c>area.action.outcome</c>. Messages carry identifiers and outcomes only: never book titles,
/// file names, secrets or personal data.
/// </summary>
public static partial class AppLog
{
    /// <summary>An exception escaped every handler; the process is terminating.</summary>
    [LoggerMessage(EventId = 1001, EventName = "app.crash.unhandled", Level = LogLevel.Critical,
        Message = "Unhandled exception; Ogma is terminating")]
    public static partial void UnhandledCrash(ILogger logger, Exception exception);

    /// <summary>A background task failed and nobody observed it.</summary>
    [LoggerMessage(EventId = 1002, EventName = "app.task.unobserved", Level = LogLevel.Error,
        Message = "A background task failed without being observed")]
    public static partial void UnobservedTask(ILogger logger, Exception exception);

    /// <summary>The UI dispatcher recovered from an exception.</summary>
    [LoggerMessage(EventId = 1003, EventName = "app.dispatcher.recovered", Level = LogLevel.Error,
        Message = "An exception escaped the UI thread and was recovered")]
    public static partial void DispatcherRecovered(ILogger logger, Exception exception);

    /// <summary>A fatal exception escaped the UI dispatcher.</summary>
    [LoggerMessage(EventId = 1004, EventName = "app.dispatcher.fatal", Level = LogLevel.Critical,
        Message = "A fatal exception escaped the UI thread")]
    public static partial void DispatcherFatal(ILogger logger, Exception exception);

    /// <summary>A failure notice itself could not be shown.</summary>
    [LoggerMessage(EventId = 1005, EventName = "app.notification.failed", Level = LogLevel.Error,
        Message = "The failure notice for {Operation} could not be shown")]
    public static partial void NotificationFailed(ILogger logger, Exception exception, string operation);

    /// <summary>A second launch asked this instance to come forward.</summary>
    [LoggerMessage(EventId = 1101, EventName = "app.instance.activation.received", Level = LogLevel.Information,
        Message = "A second launch asked this instance to come forward")]
    public static partial void ActivationReceived(ILogger logger);

    /// <summary>The single-instance pipe failed.</summary>
    [LoggerMessage(EventId = 1102, EventName = "app.instance.activation.failed", Level = LogLevel.Warning,
        Message = "The single-instance pipe failed; listening again")]
    public static partial void ActivationFailed(ILogger logger, Exception exception);

    /// <summary>An activation message was malformed.</summary>
    [LoggerMessage(EventId = 1103, EventName = "app.instance.activation.rejected", Level = LogLevel.Warning,
        Message = "An activation message with an unknown header was ignored")]
    public static partial void ActivationRejected(ILogger logger);

    /// <summary>A diagnostics bundle was written.</summary>
    [LoggerMessage(EventId = 1201, EventName = "diagnostics.export.completed", Level = LogLevel.Information,
        Message = "Diagnostics bundle exported")]
    public static partial void DiagnosticsExported(ILogger logger);

    /// <summary>A diagnostics bundle could not be written.</summary>
    [LoggerMessage(EventId = 1202, EventName = "diagnostics.export.failed", Level = LogLevel.Error,
        Message = "Diagnostics bundle export failed")]
    public static partial void DiagnosticsExportFailed(ILogger logger, Exception exception);

    /// <summary>The process started.</summary>
    [LoggerMessage(EventId = 1301, EventName = "app.start.completed", Level = LogLevel.Information,
        Message = "Ogma {Version} starting (guard {GuardMode})")]
    public static partial void AppStarted(ILogger logger, string version, string guardMode);

    /// <summary>The process exited normally.</summary>
    [LoggerMessage(EventId = 1302, EventName = "app.exit.completed", Level = LogLevel.Information,
        Message = "Ogma exited")]
    public static partial void AppExited(ILogger logger);

    /// <summary>Composition failed.</summary>
    [LoggerMessage(EventId = 1401, EventName = "app.composition.failed", Level = LogLevel.Error,
        Message = "Application composition failed ({FailureCode})")]
    public static partial void CompositionFailed(ILogger logger, Exception exception, string failureCode);

    /// <summary>Cleanup after a failed composition did not finish.</summary>
    [LoggerMessage(EventId = 1402, EventName = "app.composition.cleanup_failed", Level = LogLevel.Warning,
        Message = "Cleanup after a failed composition did not finish")]
    public static partial void CompositionCleanupFailed(ILogger logger, Exception exception);

    /// <summary>The E2E fault injection fired.</summary>
    [LoggerMessage(EventId = 1403, EventName = "app.e2e.fault_injected", Level = LogLevel.Warning,
        Message = "Injecting an E2E fault for the resilience journey")]
    public static partial void FaultInjected(ILogger logger);

    /// <summary>The library root could not be added to redaction.</summary>
    [LoggerMessage(EventId = 1404, EventName = "app.redaction.roots_failed", Level = LogLevel.Warning,
        Message = "The library root could not be added to log redaction")]
    public static partial void RedactionRootsFailed(ILogger logger, Exception exception);

    /// <summary>Startup composition completed.</summary>
    [LoggerMessage(EventId = 1405, EventName = "app.composition.completed", Level = LogLevel.Information,
        Message = "Application services composed")]
    public static partial void CompositionCompleted(ILogger logger);

    /// <summary>A startup task failed.</summary>
    [LoggerMessage(EventId = 1406, EventName = "startup.task.failed", Level = LogLevel.Error,
        Message = "Startup task {TaskName} failed ({FailureCode})")]
    public static partial void StartupTaskFailed(ILogger logger, Exception exception, string taskName, string failureCode);

    /// <summary>The startup capability probe failed.</summary>
    [LoggerMessage(EventId = 1407, EventName = "startup.capabilities.failed", Level = LogLevel.Warning,
        Message = "Startup capability checks failed")]
    public static partial void CapabilityProbeFailed(ILogger logger, Exception exception);

    /// <summary>A UI action failed and was reported.</summary>
    [LoggerMessage(EventId = 2001, EventName = "ui.action.failed", Level = LogLevel.Error,
        Message = "UI action {Operation} ({Caller}) failed")]
    public static partial void UiActionFailed(ILogger logger, Exception exception, string operation, string caller);

    /// <summary>A UI action was cancelled.</summary>
    [LoggerMessage(EventId = 2002, EventName = "ui.action.cancelled", Level = LogLevel.Debug,
        Message = "UI action {Operation} ({Caller}) was cancelled")]
    public static partial void UiActionCancelled(ILogger logger, string operation, string caller);

    /// <summary>A UI action failed fatally.</summary>
    [LoggerMessage(EventId = 2003, EventName = "ui.action.fatal", Level = LogLevel.Critical,
        Message = "UI action {Operation} ({Caller}) failed fatally")]
    public static partial void UiActionFatal(ILogger logger, Exception exception, string operation, string caller);

    /// <summary>A view model was changed off the UI thread.</summary>
    [LoggerMessage(EventId = 2101, EventName = "ui.thread.violation", Level = LogLevel.Error,
        Message = "View model {ViewModel}.{Property} changed off the UI thread")]
    public static partial void UiThreadViolation(ILogger logger, string viewModel, string property);

    /// <summary>The catalogue could not be loaded.</summary>
    [LoggerMessage(EventId = 3001, EventName = "catalogue.load.failed", Level = LogLevel.Error,
        Message = "The catalogue could not be loaded")]
    public static partial void CatalogueLoadFailed(ILogger logger, Exception exception);

    /// <summary>A catalogue refresh was superseded by a newer one.</summary>
    [LoggerMessage(EventId = 3002, EventName = "catalogue.load.superseded", Level = LogLevel.Debug,
        Message = "A catalogue refresh was superseded by a newer refresh")]
    public static partial void CatalogueLoadSuperseded(ILogger logger);

    /// <summary>A reader session could not be opened.</summary>
    [LoggerMessage(EventId = 4001, EventName = "reader.open.failed", Level = LogLevel.Error,
        Message = "The reader could not open the selected book")]
    public static partial void ReaderOpenFailed(ILogger logger, Exception exception);

    /// <summary>A page render failed; the reader shows its retry state.</summary>
    [LoggerMessage(EventId = 4002, EventName = "reader.render.failed", Level = LogLevel.Warning,
        Message = "Rendering page {PageIndex} failed")]
    public static partial void ReaderRenderFailed(ILogger logger, Exception exception, int pageIndex);

    /// <summary>Closing a reader session failed; the shell still returned to the library.</summary>
    [LoggerMessage(EventId = 4003, EventName = "reader.close.failed", Level = LogLevel.Warning,
        Message = "Closing the reader session did not finish cleanly")]
    public static partial void ReaderCloseFailed(ILogger logger, Exception exception);

    /// <summary>A view-model operation failed and was turned into a visible error state.</summary>
    [LoggerMessage(EventId = 3003, EventName = "viewmodel.operation.failed", Level = LogLevel.Error,
        Message = "{ViewModel} operation {Operation} failed")]
    public static partial void ViewModelOperationFailed(ILogger logger, Exception exception, string viewModel, string operation);

    /// <summary>A best-effort view-model step failed and was intentionally ignored.</summary>
    [LoggerMessage(EventId = 3004, EventName = "viewmodel.operation.ignored", Level = LogLevel.Debug,
        Message = "{ViewModel} best-effort step {Operation} failed and was ignored")]
    public static partial void ViewModelStepIgnored(ILogger logger, Exception exception, string viewModel, string operation);
}
