using System.Globalization;
using System.Text.Json;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>A redacted record of the last unexpected process exit.</summary>
/// <param name="TimestampUtc">When the crash was recorded.</param>
/// <param name="ExceptionType">The exception's full type name.</param>
/// <param name="Stack">The redacted exception detail.</param>
/// <param name="AppVersion">The application version that crashed.</param>
public sealed record CrashMarkerRecord(
    DateTimeOffset TimestampUtc,
    string ExceptionType,
    string Stack,
    string AppVersion);

/// <summary>
/// Reads and writes <c>&lt;data&gt;/logs/last-crash.json</c> (Sept-23 Phase 02, T02.1). The
/// marker is written by the process-wide handler and consumed on the next launch to show a
/// one-time recovery notice. All content is redacted before it reaches disk.
/// </summary>
public static class CrashMarker
{
    /// <summary>The marker file name inside the logs directory.</summary>
    public const string FileName = "last-crash.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Writes the marker. Never throws; returns whether the marker was written.</summary>
    /// <param name="logsDirectory">The logs directory.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="appVersion">The running application version.</param>
    /// <param name="redactor">The redaction policy.</param>
    /// <param name="timeProvider">Optional clock.</param>
    /// <returns><see langword="true"/> when the marker reached disk.</returns>
    public static bool TryWrite(
        string logsDirectory,
        Exception exception,
        string appVersion,
        LogRedactor redactor,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(redactor);
        try
        {
            Directory.CreateDirectory(logsDirectory);
            var record = new CrashMarkerRecord(
                (timeProvider ?? TimeProvider.System).GetUtcNow(),
                exception.GetType().FullName ?? exception.GetType().Name,
                redactor.Redact(exception.ToString()),
                appVersion);
            File.WriteAllText(
                Path.Combine(logsDirectory, FileName),
                JsonSerializer.Serialize(record, JsonOptions));
            return true;
        }
        catch (Exception writeFailure) when (writeFailure is IOException or
                                             UnauthorizedAccessException or
                                             NotSupportedException)
        {
            // Intentionally ignored: the process is already terminating; the log entry remains.
            System.Diagnostics.Trace.WriteLine("ogma crash marker failed: " + writeFailure.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Reads and acknowledges the marker so the recovery notice appears only once. The marker is
    /// renamed to <c>crash-&lt;timestamp&gt;.json</c> so diagnostics exports still include it.
    /// </summary>
    /// <param name="logsDirectory">The logs directory.</param>
    /// <returns>The marker, or <see langword="null"/> when none exists or it cannot be read.</returns>
    public static CrashMarkerRecord? TryConsume(string logsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        string path = Path.Combine(logsDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        CrashMarkerRecord? record = null;
        try
        {
            record = JsonSerializer.Deserialize<CrashMarkerRecord>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            // A torn marker still means the last run ended unexpectedly.
            record = new CrashMarkerRecord(File.GetLastWriteTimeUtc(path), "unknown", string.Empty, string.Empty);
        }
        catch (Exception readFailure) when (readFailure is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine("ogma crash marker unreadable: " + readFailure.GetType().Name);
            return null;
        }

        try
        {
            string archived = Path.Combine(
                logsDirectory,
                "crash-" + (record?.TimestampUtc ?? DateTimeOffset.UtcNow)
                    .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.Move(path, archived, overwrite: true);
        }
        catch (Exception moveFailure) when (moveFailure is IOException or UnauthorizedAccessException)
        {
            // Without the rename the notice would repeat; delete as the fallback.
            System.Diagnostics.Trace.WriteLine("ogma crash marker archive failed: " + moveFailure.GetType().Name);
            TryDelete(path);
        }

        return record;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception deleteFailure) when (deleteFailure is IOException or UnauthorizedAccessException)
        {
            // Intentionally ignored: a read-only logs folder can only repeat the one-time notice.
            System.Diagnostics.Trace.WriteLine("ogma crash marker delete failed: " + deleteFailure.GetType().Name);
        }
    }
}
