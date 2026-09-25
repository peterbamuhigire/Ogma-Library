using System.Globalization;
using System.IO.Compression;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>
/// Builds the redacted <em>Export diagnostics</em> bundle (Sept-23 Phase 02, T02.3): a zip in
/// <c>&lt;data&gt;/diagnostics/</c> containing the rolling logs, crash markers and any extra
/// documents the caller provides. Log content is already redacted when it is written.
/// </summary>
public static class DiagnosticsBundleWriter
{
    /// <summary>The logs folder name under the data directory.</summary>
    public const string LogsFolderName = "logs";

    /// <summary>Returns the logs directory for <paramref name="dataDirectory"/>.</summary>
    /// <param name="dataDirectory">The Ogma data directory.</param>
    /// <returns>The logs directory path.</returns>
    public static string GetLogsDirectory(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        return Path.Combine(dataDirectory, LogsFolderName);
    }

    /// <summary>Writes a diagnostics zip and returns its path.</summary>
    /// <param name="dataDirectory">The Ogma data directory.</param>
    /// <param name="extraEntries">Additional redacted documents keyed by entry name.</param>
    /// <param name="timeProvider">Optional clock used for the bundle name.</param>
    /// <param name="cancellationToken">A token to cancel the export.</param>
    /// <returns>The absolute path of the created zip.</returns>
    public static async Task<string> CreateAsync(
        string dataDirectory,
        IReadOnlyDictionary<string, string>? extraEntries = null,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        string directory = Path.Combine(dataDirectory, "diagnostics");
        Directory.CreateDirectory(directory);
        string stamp = (timeProvider ?? TimeProvider.System).GetUtcNow()
            .ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, $"ogma-diagnostics-{stamp}.zip");

        var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using (output.ConfigureAwait(false))
        {
            using var archive = new ZipArchive(output, ZipArchiveMode.Create);
            string logs = GetLogsDirectory(dataDirectory);
            if (Directory.Exists(logs))
            {
                foreach (string file in Directory.EnumerateFiles(logs)
                             .Where(file => file.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                                            file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ZipArchiveEntry entry = archive.CreateEntry(
                        LogsFolderName + "/" + Path.GetFileName(file),
                        CompressionLevel.Optimal);
                    Stream target = entry.Open();
                    await using (target.ConfigureAwait(false))
                    {
                        // The active log is open for append; share read/write to copy it.
                        var source = new FileStream(
                            file,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete);
                        await using (source.ConfigureAwait(false))
                        {
                            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            foreach ((string name, string content) in extraEntries ?? new Dictionary<string, string>())
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                Stream target = entry.Open();
                await using (target.ConfigureAwait(false))
                {
                    var writer = new StreamWriter(target);
                    await using (writer.ConfigureAwait(false))
                    {
                        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        return path;
    }
}
