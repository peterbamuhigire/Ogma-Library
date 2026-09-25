using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// The Phase 02 safe-invoke pattern for UI event handlers (Sept-23, T02.2, K31). An Avalonia
/// event handler cannot be awaited, so the only permitted <c>async void</c> in the App is
/// <see cref="Run"/>: it awaits the action, logs every failure with the operation name and
/// reports a localised, dismissible message instead of letting the exception terminate the
/// process. Cancellation is logged at Debug and never shown.
/// </summary>
/// <example>
/// <code>
/// private void NextButton_Click(object? sender, RoutedEventArgs e) =>
///     UiActions.Run(() => NextAsync(), "reader.next");
/// </code>
/// </example>
public static class UiActions
{
    /// <summary>The localisation key shown when a UI action fails.</summary>
    public const string ActionFailedMessageKey = "Notification.ActionFailed";

    private static ILogger _logger = NullLogger.Instance;
    private static IUserNotifier _notifier = NullUserNotifier.Instance;

    /// <summary>Connects the helper to the process logger and the notification surface.</summary>
    /// <param name="logger">The logger for UI action failures.</param>
    /// <param name="notifier">The notification surface.</param>
    public static void Configure(ILogger logger, IUserNotifier notifier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    /// <summary>
    /// Runs <paramref name="action"/> from a UI event handler. The action starts synchronously
    /// on the caller's (UI) thread, so code before its first <c>await</c> can still set
    /// <c>e.Handled</c>. Failures are logged and reported; fatal failures are rethrown.
    /// </summary>
    /// <param name="action">The asynchronous work of the handler.</param>
    /// <param name="operation">A stable <c>area.action</c> name for logs.</param>
    /// <param name="caller">The invoking member (filled by the compiler).</param>
    public static async void Run(
        Func<Task> action,
        string operation,
        [CallerMemberName] string caller = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await RunAsync(action, operation, caller).ConfigureAwait(true);
        }
        catch (Exception exception) when (ExceptionClassification.IsFatal(exception))
        {
            // Fatal state is never absorbed; the global handler records the crash.
            throw;
        }
    }

    /// <summary>
    /// The awaitable core of <see cref="Run"/>: never throws for non-fatal failures. Exposed for
    /// commands and tests.
    /// </summary>
    /// <param name="action">The asynchronous work.</param>
    /// <param name="operation">A stable <c>area.action</c> name for logs.</param>
    /// <param name="caller">The invoking member (filled by the compiler).</param>
    /// <returns><see langword="true"/> when the action completed successfully.</returns>
    public static async Task<bool> RunAsync(
        Func<Task> action,
        string operation,
        [CallerMemberName] string caller = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action().ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            AppLog.UiActionCancelled(_logger, operation, caller);
            return false;
        }
        catch (Exception exception) when (ExceptionClassification.IsFatal(exception))
        {
            AppLog.UiActionFatal(_logger, exception, operation, caller);
            throw;
        }
        catch (Exception exception)
        {
            Report(exception, operation, caller);
            return false;
        }
    }

    /// <summary>Logs a handled UI failure and shows the generic, localised failure message.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="operation">A stable <c>area.action</c> name for logs.</param>
    /// <param name="caller">The invoking member.</param>
    public static void Report(Exception exception, string operation, string caller = "")
    {
        ArgumentNullException.ThrowIfNull(exception);
        AppLog.UiActionFailed(_logger, exception, operation, caller);
        try
        {
            _notifier.Notify(new UserNotification(
                ActionFailedMessageKey,
                UserNotificationSeverity.Error,
                UserNotificationActionKind.ExportDiagnostics));
        }
        catch (Exception notifyFailure) when (!ExceptionClassification.IsFatal(notifyFailure))
        {
            AppLog.NotificationFailed(_logger, notifyFailure, operation);
        }
    }
}
