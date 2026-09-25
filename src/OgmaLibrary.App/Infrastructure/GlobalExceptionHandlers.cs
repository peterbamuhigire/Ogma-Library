using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// The process-wide safety net (Sept-23 Phase 02, T02.1, K31). It routes every exception that
/// escapes application code to the log and, where the process can continue, to a localised
/// notification instead of terminating Ogma.
/// </summary>
public static class GlobalExceptionHandlers
{
    /// <summary>The localisation key shown when the dispatcher recovers from an exception.</summary>
    public const string UnexpectedErrorMessageKey = "Notification.UnexpectedError";

    private static bool _processHandlersInstalled;

    /// <summary>
    /// Installs the <see cref="AppDomain.UnhandledException"/> and
    /// <see cref="TaskScheduler.UnobservedTaskException"/> handlers. Call once, before Avalonia starts.
    /// </summary>
    public static void InstallProcessHandlers()
    {
        if (_processHandlersInstalled)
        {
            return;
        }

        _processHandlersInstalled = true;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>Installs the Avalonia UI-thread handler. Call once the dispatcher exists.</summary>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="notifier">The notification surface.</param>
    public static void InstallDispatcherHandler(Dispatcher dispatcher, IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(notifier);
        dispatcher.UnhandledException += (_, e) =>
            e.Handled = HandleDispatcherException(
                e.Exception,
                AppDiagnostics.CreateLogger<App>(),
                notifier);
    }

    /// <summary>
    /// Handles an exception that escaped a UI-thread callback. Non-fatal exceptions are logged,
    /// reported and marked handled so the app survives; fatal ones are logged and left unhandled.
    /// </summary>
    /// <param name="exception">The escaped exception.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="notifier">The notification surface.</param>
    /// <returns><see langword="true"/> when the exception was absorbed.</returns>
    public static bool HandleDispatcherException(Exception exception, ILogger logger, IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(notifier);
        if (ExceptionClassification.IsFatal(exception))
        {
            AppLog.DispatcherFatal(logger, exception);
            return false;
        }

        AppLog.DispatcherRecovered(logger, exception);
        try
        {
            notifier.Notify(new UserNotification(
                UnexpectedErrorMessageKey,
                UserNotificationSeverity.Error,
                UserNotificationActionKind.ExportDiagnostics));
        }
        catch (Exception notifyFailure) when (!ExceptionClassification.IsFatal(notifyFailure))
        {
            AppLog.NotificationFailed(logger, notifyFailure, "app.dispatcher.recovered");
        }

        return true;
    }

    /// <summary>Handles an unobserved task exception: logs it at Error and marks it observed.</summary>
    /// <param name="exception">The aggregate exception.</param>
    /// <param name="logger">The logger.</param>
    public static void HandleUnobservedTaskException(AggregateException exception, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);
        AppLog.UnobservedTask(logger, exception.Flatten());
    }

    /// <summary>Records a terminating exception: Critical log, crash marker and flush.</summary>
    /// <param name="exception">The terminating exception.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="logsDirectory">The logs directory that receives <c>last-crash.json</c>.</param>
    /// <param name="redactor">The redaction policy.</param>
    public static void RecordCrash(Exception exception, ILogger logger, string? logsDirectory, LogRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(redactor);
        AppLog.UnhandledCrash(logger, exception);
        if (logsDirectory is not null)
        {
            CrashMarker.TryWrite(logsDirectory, exception, AppDiagnostics.AppVersion, redactor);
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Exception exception = e.ExceptionObject as Exception ??
            new InvalidOperationException("A non-exception object was thrown.");
        try
        {
            RecordCrash(
                exception,
                AppDiagnostics.CreateLogger<App>(),
                AppDiagnostics.LogsDirectory,
                AppDiagnostics.Redactor);
        }
        finally
        {
            AppDiagnostics.Flush();
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleUnobservedTaskException(e.Exception, AppDiagnostics.CreateLogger<App>());
        e.SetObserved();
    }
}
