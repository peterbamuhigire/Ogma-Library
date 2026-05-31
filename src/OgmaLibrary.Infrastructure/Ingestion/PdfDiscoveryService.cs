using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Recursively enumerates *.pdf files under a library root, filtering out excluded
/// folders, and streams results via a <see cref="System.Threading.Channels.Channel{T}"/>
/// (FR-LIB-002). All enumeration runs on a background thread pool thread; the UI
/// thread is never touched. Paths outside the library root are silently skipped
/// (path-traversal guard).
/// </summary>
public sealed class PdfDiscoveryService : IPdfDiscoveryService
{
    /// <inheritdoc />
    public async Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(excludedFolders);
        ArgumentNullException.ThrowIfNull(writer);

        // Normalize root once for prefix-matching.
        string normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootPath));

        try
        {
            await Task.Run(() =>
                EnumerateFiles(normalizedRoot, excludedFolders, writer, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static void EnumerateFiles(
        string normalizedRoot,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(normalizedRoot);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string dir = stack.Pop();
            string dirName = Path.GetFileName(dir);

            if (IsExcluded(dir, dirName, normalizedRoot, excludedFolders))
            {
                continue;
            }

            // Emit PDF files in this directory.
            foreach (string file in EnumerateFilesInDir(dir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fullPath = Path.GetFullPath(file);

                // Path-traversal guard: skip anything outside the root.
                if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    continue;
                }

                string relative = ComputeRelativePath(fullPath, normalizedRoot);
                var discovered = new DiscoveredFile(
                    AbsolutePath: fullPath,
                    RelativePath: relative,
                    SizeBytes: info.Length,
                    MtimeTicks: info.LastWriteTimeUtc.Ticks);

                writer.TryWrite(discovered);
            }

            // Push sub-directories onto the stack.
            foreach (string sub in EnumerateSubDirs(dir))
            {
                stack.Push(sub);
            }
        }
    }

    private static bool IsExcluded(
        string dirAbsolute,
        string dirName,
        string normalizedRoot,
        IReadOnlyList<string> excludedFolders)
    {
        foreach (string excluded in excludedFolders)
        {
            if (string.IsNullOrWhiteSpace(excluded))
            {
                continue;
            }

            // Match by directory name (e.g., "excluded").
            if (string.Equals(dirName, excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Match by relative path prefix (forward-slash normalised).
            string relDir = ComputeRelativePath(dirAbsolute, normalizedRoot);
            string normalizedExcluded = excluded.Replace('\\', '/').TrimStart('/');
            if (relDir.StartsWith(normalizedExcluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateFilesInDir(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateSubDirs(string dir)
    {
        try
        {
            return Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static string ComputeRelativePath(string absolutePath, string normalizedRoot)
    {
        string root = normalizedRoot + Path.DirectorySeparatorChar;
        if (absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }

        return absolutePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
