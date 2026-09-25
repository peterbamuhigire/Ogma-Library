using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// The <em>Export diagnostics</em> action used by toasts and the crash-recovery notice
/// (Sept-23 Phase 02, T02.1/T02.3). It writes the redacted logs and crash markers into
/// <c>&lt;data&gt;/diagnostics/</c> and reports the outcome as a localised toast.
/// </summary>
public static class DiagnosticsExport
{
    /// <summary>Exports the diagnostics bundle and notifies the user of the result.</summary>
    /// <param name="notifier">The notification surface.</param>
    /// <param name="extraEntries">Optional additional redacted documents.</param>
    /// <param name="cancellationToken">A token to cancel the export.</param>
    /// <returns>The bundle path, or <see langword="null"/> when the export failed.</returns>
    public static async Task<string?> ExportAsync(
        IUserNotifier notifier,
        IReadOnlyDictionary<string, string>? extraEntries = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ILogger logger = AppDiagnostics.CreateLogger<App>();
        string? dataDirectory = AppDiagnostics.DataDirectory;
        if (dataDirectory is null)
        {
            notifier.Notify(new UserNotification("Notification.DiagnosticsExportFailed"));
            return null;
        }

        try
        {
            AppDiagnostics.Flush();
            string path = await DiagnosticsBundleWriter.CreateAsync(
                dataDirectory,
                extraEntries,
                cancellationToken: cancellationToken).ConfigureAwait(true);
            AppLog.DiagnosticsExported(logger);
            notifier.Notify(new UserNotification(
                "Notification.DiagnosticsExported",
                UserNotificationSeverity.Information));
            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLog.DiagnosticsExportFailed(logger, exception);
            notifier.Notify(new UserNotification("Notification.DiagnosticsExportFailed"));
            return null;
        }
    }
}
