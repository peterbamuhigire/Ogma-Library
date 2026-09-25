using Microsoft.Data.Sqlite;
using OgmaLibrary.App.Configuration;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>Why application composition or startup failed (Sept-23 Phase 02, T02.8).</summary>
public enum StartupFailureKind
{
    /// <summary>The cause is not recognised.</summary>
    Unknown = 0,

    /// <summary>Environment or runtime configuration is invalid.</summary>
    Configuration = 1,

    /// <summary>The catalogue database is locked by another process.</summary>
    DatabaseLocked = 2,

    /// <summary>The catalogue database file is damaged or not a database.</summary>
    DatabaseCorrupt = 3,

    /// <summary>The catalogue schema could not be migrated.</summary>
    Migration = 4,

    /// <summary>The data folder cannot be read or written (permissions, missing or full disk).</summary>
    StorageUnavailable = 5,
}

/// <summary>Maps a startup exception to a <see cref="StartupFailureKind"/> and its user message.</summary>
public static class StartupFailureClassifier
{
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;
    private const int DiskFullHResult = unchecked((int)0x80070070);

    /// <summary>Classifies <paramref name="exception"/> by walking it and its inner exceptions.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The failure class.</returns>
    public static StartupFailureKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (Exception candidate in Flatten(exception))
        {
            switch (candidate)
            {
                case OgmaConfigurationException:
                    return StartupFailureKind.Configuration;
                case SqliteException sqlite when sqlite.SqliteErrorCode is SqliteBusy or SqliteLocked:
                    return StartupFailureKind.DatabaseLocked;
                case SqliteException sqlite when sqlite.SqliteErrorCode is SqliteCorrupt or SqliteNotADatabase:
                    return StartupFailureKind.DatabaseCorrupt;
                case InvalidDataException:
                    return StartupFailureKind.DatabaseCorrupt;
                case UnauthorizedAccessException:
                case DirectoryNotFoundException:
                case IOException io when io.HResult == DiskFullHResult:
                    return StartupFailureKind.StorageUnavailable;
            }

            if (candidate.GetType().FullName?.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) == true ||
                candidate.StackTrace?.Contains("CatalogueMigrator", StringComparison.Ordinal) == true)
            {
                return StartupFailureKind.Migration;
            }
        }

        return exception is IOException ? StartupFailureKind.StorageUnavailable : StartupFailureKind.Unknown;
    }

    /// <summary>Returns a stable diagnostic code for <paramref name="kind"/>.</summary>
    /// <param name="kind">The failure class.</param>
    /// <returns>A snake-case code safe for logs and exports.</returns>
    public static string ToCode(StartupFailureKind kind) => kind switch
    {
        StartupFailureKind.Configuration => "configuration_unusable",
        StartupFailureKind.DatabaseLocked => "database_locked",
        StartupFailureKind.DatabaseCorrupt => "database_corrupt",
        StartupFailureKind.Migration => "migration_failed",
        StartupFailureKind.StorageUnavailable => "storage_unavailable",
        _ => "startup_failed",
    };

    /// <summary>Returns the localisation key of the message for <paramref name="kind"/>.</summary>
    /// <param name="kind">The failure class.</param>
    /// <returns>The message key.</returns>
    public static string MessageKey(StartupFailureKind kind) => "Startup.Failure." + kind;

    /// <summary>Whether rerunning composition can help for <paramref name="kind"/>.</summary>
    /// <param name="kind">The failure class.</param>
    /// <returns><see langword="false"/> only for a damaged database, where retrying repeats the failure.</returns>
    public static bool CanRetry(StartupFailureKind kind) => kind != StartupFailureKind.DatabaseCorrupt;

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        int visited = 0;
        while (pending.Count > 0 && visited++ < 32)
        {
            Exception current = pending.Pop();
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }
    }
}
