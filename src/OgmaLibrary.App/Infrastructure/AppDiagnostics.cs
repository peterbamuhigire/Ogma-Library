using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// Process-level logging bootstrap (Sept-23 Phase 02, T02.1/T02.3). It is created in
/// <see cref="Program.Main"/> before Avalonia starts so the global safety net can log even when
/// composition fails. The composition root registers the same <see cref="ILoggerFactory"/> so
/// every service logs to one redacted rolling file under <c>&lt;data&gt;/logs</c>.
/// </summary>
public static class AppDiagnostics
{
    private static RollingFileLoggerProvider? _fileProvider;
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>The process logger factory (a null factory until <see cref="Initialize"/> runs).</summary>
    public static ILoggerFactory LoggerFactory => _loggerFactory;

    /// <summary>The redaction policy shared by the log sink and crash marker.</summary>
    public static LogRedactor Redactor { get; private set; } = new();

    /// <summary>The logs directory, once initialised.</summary>
    public static string? LogsDirectory { get; private set; }

    /// <summary>The data directory that owns the logs, once initialised.</summary>
    public static string? DataDirectory { get; private set; }

    /// <summary>The informational application version recorded in crash markers.</summary>
    public static string AppVersion { get; } =
        typeof(AppDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        typeof(AppDiagnostics).Assembly.GetName().Version?.ToString() ??
        "unknown";

    /// <summary>Creates the rolling file sink for <paramref name="dataDirectory"/>.</summary>
    /// <param name="dataDirectory">The Ogma data directory.</param>
    /// <param name="libraryRoots">Library roots whose paths must be redacted.</param>
    /// <param name="minimumLevel">The lowest level written to disk.</param>
    public static void Initialize(
        string dataDirectory,
        IEnumerable<string>? libraryRoots = null,
        LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Shutdown();
        DataDirectory = dataDirectory;
        LogsDirectory = DiagnosticsBundleWriter.GetLogsDirectory(dataDirectory);
        Redactor = new LogRedactor(libraryRoots: libraryRoots);
        _fileProvider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions
            {
                DirectoryPath = LogsDirectory,
                MinimumLevel = minimumLevel,
            },
            Redactor);
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            // Framework chatter (HTTP request lines, EF Core commands) can carry URLs and SQL;
            // only its warnings and errors are kept.
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddProvider(_fileProvider);
        });
    }

    /// <summary>Creates a logger for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The category type.</typeparam>
    /// <returns>A logger that writes to the rolling sink.</returns>
    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>Flushes pending log bytes to disk (used on the crash path).</summary>
    public static void Flush() => _fileProvider?.Flush();

    /// <summary>Flushes and releases the sink.</summary>
    public static void Shutdown()
    {
        ILoggerFactory factory = _loggerFactory;
        _loggerFactory = NullLoggerFactory.Instance;
        _fileProvider?.Flush();
        if (!ReferenceEquals(factory, NullLoggerFactory.Instance))
        {
            factory.Dispose();
        }

        _fileProvider?.Dispose();
        _fileProvider = null;
    }
}
