using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Tests.Diagnostics;

/// <summary>Sept-23 Phase 02 (T02.1, T02.3): the rolling sink, crash marker and diagnostics bundle.</summary>
public sealed class RollingFileLoggerProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ogma-p02-logs-" + Guid.NewGuid().ToString("N"));
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero));

    public RollingFileLoggerProviderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Log_WritesRedactedJsonLineWithEventName()
    {
        string logs = Path.Combine(_root, "logs");
        string library = Path.Combine(_root, "library");
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions { DirectoryPath = logs, TimeProvider = _clock },
            new LogRedactor(libraryRoots: [library]));
        ILogger logger = provider.CreateLogger("OgmaLibrary.Tests");

        logger.Log(
            LogLevel.Error,
            new EventId(4002, "reader.render.failed"),
            Path.Combine(library, "Shelf", "Moby Dick.pdf"),
            new IOException("The pipe is being closed."),
            (state, _) => "Rendering " + state + " failed");
        provider.Flush();

        string path = Path.Combine(logs, "ogma-20260925.log");
        string line = Assert.Single(ReadAllLinesShared(path));
        using JsonDocument json = JsonDocument.Parse(line);
        Assert.Equal("reader.render.failed", json.RootElement.GetProperty("event").GetString());
        Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
        Assert.Equal("System.IO.IOException", json.RootElement.GetProperty("exception").GetProperty("type").GetString());
        Assert.DoesNotContain("Moby Dick", line, StringComparison.Ordinal);
        Assert.DoesNotContain(library.Replace("\\", "\\\\", StringComparison.Ordinal), line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsNotWritten()
    {
        string logs = Path.Combine(_root, "logs");
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions { DirectoryPath = logs, TimeProvider = _clock },
            new LogRedactor());

        provider.CreateLogger("x").Log(LogLevel.Debug, default, "quiet", null, (s, _) => s);

        Assert.False(Directory.Exists(logs) && Directory.EnumerateFiles(logs).Any());
    }

    [Fact]
    public void Log_PastSizeLimit_RollsAndKeepsOnlyRetainedFiles()
    {
        string logs = Path.Combine(_root, "logs");
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions
            {
                DirectoryPath = logs,
                MaxFileSizeBytes = 2048,
                MaxRetainedFiles = 3,
                TimeProvider = _clock,
            },
            new LogRedactor());
        ILogger logger = provider.CreateLogger("OgmaLibrary.Tests");

        for (int index = 0; index < 200; index++)
        {
            logger.Log(LogLevel.Information, default, index, null, (i, _) => $"entry {i} {new string('x', 80)}");
        }

        string[] files = Directory.GetFiles(logs, "ogma-*.log");
        Assert.InRange(files.Length, 2, 3);
        Assert.All(files, file => Assert.True(new FileInfo(file).Length <= 2048));
    }

    [Fact]
    public void Log_WhenLogFolderCannotBeCreated_FallsBackWithoutThrowing()
    {
        string blocker = Path.Combine(_root, "not-a-folder");
        File.WriteAllText(blocker, "a file where the logs folder should be");
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions { DirectoryPath = Path.Combine(blocker, "logs"), TimeProvider = _clock },
            new LogRedactor());

        Exception? thrown = Record.Exception(() =>
            provider.CreateLogger("x").Log(LogLevel.Error, default, "disk problem", null, (s, _) => s));

        Assert.Null(thrown);
        Assert.Equal(1, provider.FallbackCount);
    }

    [Fact]
    public void CrashMarker_IsWrittenRedactedAndConsumedOnce()
    {
        string logs = Path.Combine(_root, "logs");
        var redactor = new LogRedactor(homeDirectory: @"C:\Users\Reader");
        var exception = new InvalidOperationException(@"Failed at C:\Users\Reader\Books\Secret Title.pdf");

        Assert.True(CrashMarker.TryWrite(logs, exception, "1.2.3", redactor, _clock));
        string markerText = File.ReadAllText(Path.Combine(logs, CrashMarker.FileName));
        Assert.DoesNotContain("Secret Title", markerText, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Users\\Reader", markerText, StringComparison.Ordinal);

        CrashMarkerRecord? first = CrashMarker.TryConsume(logs);
        CrashMarkerRecord? second = CrashMarker.TryConsume(logs);

        Assert.NotNull(first);
        Assert.Equal(typeof(InvalidOperationException).FullName, first!.ExceptionType);
        Assert.Equal("1.2.3", first.AppVersion);
        Assert.Null(second);
        Assert.Single(Directory.GetFiles(logs, "crash-*.json"));
    }

    [Fact]
    public async Task DiagnosticsBundle_IncludesOpenLogsAndExtraEntries()
    {
        string logs = DiagnosticsBundleWriter.GetLogsDirectory(_root);
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions { DirectoryPath = logs, TimeProvider = _clock },
            new LogRedactor());
        provider.CreateLogger("x").Log(LogLevel.Warning, default, "still open while exporting", null, (s, _) => s);

        string bundle = await DiagnosticsBundleWriter.CreateAsync(
            _root,
            new Dictionary<string, string> { ["startup.json"] = "{}" },
            _clock);

        using ZipArchive archive = ZipFile.OpenRead(bundle);
        Assert.Contains(archive.Entries, entry => entry.FullName == "logs/ogma-20260925.log" && entry.Length > 0);
        Assert.Contains(archive.Entries, entry => entry.FullName == "startup.json");
        Assert.StartsWith(Path.Combine(_root, "diagnostics"), bundle, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ReadAllLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
