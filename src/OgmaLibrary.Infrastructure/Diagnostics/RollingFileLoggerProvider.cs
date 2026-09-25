using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>Options for <see cref="RollingFileLoggerProvider"/>.</summary>
public sealed record RollingFileLoggerOptions
{
    /// <summary>The directory that receives <c>ogma-YYYYMMDD.log</c> files.</summary>
    public required string DirectoryPath { get; init; }

    /// <summary>The largest size a single log file may reach before rolling (default 10 MB).</summary>
    public long MaxFileSizeBytes { get; init; } = 10L * 1024 * 1024;

    /// <summary>The number of log files retained (default 7).</summary>
    public int MaxRetainedFiles { get; init; } = 7;

    /// <summary>The lowest level written (default <see cref="LogLevel.Information"/>).</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    /// <summary>The clock used for timestamps and daily file names.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

/// <summary>
/// An in-repo, dependency-free rolling JSON-lines file sink for
/// <see cref="Microsoft.Extensions.Logging"/> (Sept-23 Phase 02, T02.3). Each entry is one
/// redacted JSON object per line. Files roll daily and at the size limit, and only the newest
/// <see cref="RollingFileLoggerOptions.MaxRetainedFiles"/> are kept. A read-only, missing or full
/// log folder never throws into the caller: the entry falls back to
/// <see cref="Trace"/> and file writing is retried later.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    /// <summary>The prefix of every log file name.</summary>
    public const string FilePrefix = "ogma-";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly RollingFileLoggerOptions _options;
    private readonly LogRedactor _redactor;
    private readonly Lock _gate = new();
    private FileStream? _stream;
    private string? _currentPath;
    private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;
    private bool _disposed;

    /// <summary>Initializes the provider.</summary>
    /// <param name="options">The sink options.</param>
    /// <param name="redactor">The redaction policy applied to every message and exception.</param>
    public RollingFileLoggerProvider(RollingFileLoggerOptions options, LogRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DirectoryPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxRetainedFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxFileSizeBytes, 1024);
        _options = options;
        _redactor = redactor;
    }

    /// <summary>The directory that receives log files.</summary>
    public string DirectoryPath => _options.DirectoryPath;

    /// <summary>The number of entries that could not be written to disk and fell back to trace.</summary>
    public long FallbackCount { get; private set; }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RollingFileLogger(this, categoryName);

    /// <summary>Flushes buffered entries to disk (used by the crash handler).</summary>
    public void Flush()
    {
        lock (_gate)
        {
            try
            {
                _stream?.Flush(flushToDisk: true);
            }
            catch (IOException exception)
            {
                Trace.WriteLine("ogma log flush failed: " + exception.GetType().Name);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseStream();
        }
    }

    internal bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;

    internal void Write(
        string category,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        DateTimeOffset now = _options.TimeProvider.GetUtcNow();
        byte[] line = FormatLine(now, category, logLevel, eventId, message, exception);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (now < _retryAfter || !TryWrite(now, line))
            {
                FallbackCount++;
                Trace.WriteLine(Encoding.UTF8.GetString(line));
            }
        }
    }

    private byte[] FormatLine(
        DateTimeOffset now,
        string category,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("ts", now.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", logLevel.ToString());
            writer.WriteString("category", category);
            if (eventId.Id != 0)
            {
                writer.WriteNumber("eventId", eventId.Id);
            }

            if (!string.IsNullOrEmpty(eventId.Name))
            {
                writer.WriteString("event", eventId.Name);
            }

            writer.WriteNumber("pid", Environment.ProcessId);
            writer.WriteString("message", _redactor.Redact(message));
            if (exception is not null)
            {
                writer.WriteStartObject("exception");
                writer.WriteString("type", exception.GetType().FullName);
                writer.WriteNumber("hresult", exception.HResult);
                writer.WriteString("detail", _redactor.Redact(exception.ToString()));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    private bool TryWrite(DateTimeOffset now, byte[] line)
    {
        try
        {
            FileStream stream = EnsureStream(now, line.Length);
            stream.Write(line);
            stream.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException or
                                          System.Security.SecurityException)
        {
            // A read-only, missing or full folder must never crash the app (Phase 02 §8).
            CloseStream();
            _retryAfter = now + RetryDelay;
            Trace.WriteLine("ogma log sink unavailable: " + exception.GetType().Name);
            return false;
        }
    }

    private FileStream EnsureStream(DateTimeOffset now, int nextLength)
    {
        string dailyPath = Path.Combine(
            _options.DirectoryPath,
            FilePrefix + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
        if (_stream is not null &&
            _currentPath is not null &&
            _currentPath.StartsWith(dailyPath[..^4], StringComparison.OrdinalIgnoreCase) &&
            _stream.Length + nextLength <= _options.MaxFileSizeBytes)
        {
            return _stream;
        }

        CloseStream();
        Directory.CreateDirectory(_options.DirectoryPath);
        string day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        int latestIndex = Directory.EnumerateFiles(_options.DirectoryPath, FilePrefix + day + "*.log")
            .Select(file => RollKey(Path.GetFileName(file)))
            .Where(key => key.Day == day)
            .Select(key => key.Index)
            .DefaultIfEmpty(0)
            .Max();
        string path = PathFor(day, latestIndex);
        if (File.Exists(path) && new FileInfo(path).Length + nextLength > _options.MaxFileSizeBytes)
        {
            path = PathFor(day, latestIndex + 1);
        }

        _stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _currentPath = path;
        EnforceRetention(path);
        return _stream;
    }

    private void EnforceRetention(string activePath)
    {
        // Newest first by (day, roll index): ogma-YYYYMMDD.log precedes ogma-YYYYMMDD.1.log.
        FileInfo[] files = new DirectoryInfo(_options.DirectoryPath)
            .GetFiles(FilePrefix + "*.log")
            .Select(file => (File: file, Key: RollKey(file.Name)))
            .OrderByDescending(item => item.Key.Day, StringComparer.Ordinal)
            .ThenByDescending(item => item.Key.Index)
            .Select(item => item.File)
            .ToArray();
        foreach (FileInfo file in files.Skip(_options.MaxRetainedFiles))
        {
            if (string.Equals(file.FullName, activePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                file.Delete();
            }
            catch (IOException exception)
            {
                // Intentionally ignored: a locked old log is retried on the next roll.
                Trace.WriteLine("ogma log retention skipped a file: " + exception.GetType().Name);
            }
        }
    }

    private string PathFor(string day, int index) => Path.Combine(
        _options.DirectoryPath,
        index == 0
            ? FilePrefix + day + ".log"
            : FilePrefix + day + "." + index.ToString(CultureInfo.InvariantCulture) + ".log");

    private static (string Day, int Index) RollKey(string fileName)
    {
        // ogma-YYYYMMDD.log or ogma-YYYYMMDD.N.log
        string[] parts = fileName[FilePrefix.Length..].Split('.');
        int index = parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
        return (parts[0], index);
    }

    private void CloseStream()
    {
        try
        {
            _stream?.Dispose();
        }
        catch (IOException exception)
        {
            // Intentionally ignored: a failed close only loses buffered bytes already flushed.
            Trace.WriteLine("ogma log close failed: " + exception.GetType().Name);
        }

        _stream = null;
        _currentPath = null;
    }

    private sealed class RollingFileLogger(RollingFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);
            provider.Write(category, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}
