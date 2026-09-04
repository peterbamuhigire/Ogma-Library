using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Pathing;

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
    /// <summary>Backward-compatible overload for existing ingestion callers.</summary>
    public Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        CancellationToken cancellationToken = default) =>
        DiscoverAsync(
            rootPath,
            excludedFolders,
            writer,
            directoryDiagnosticSink: null,
            resumeAfterRelativeDirectory: null,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        Func<DiscoveryDirectoryDiagnostic, ValueTask>? directoryDiagnosticSink = null,
        string? resumeAfterRelativeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(excludedFolders);
        ArgumentNullException.ThrowIfNull(writer);

        // Canonicalize once so boundary checks remain safe for roots whose names
        // share a prefix and for existing symlink/reparse-point segments.
        string normalizedRoot = PathGuard.CanonicalizeRoot(rootPath);

        try
        {
            await Task.Run(
                () => EnumerateFilesAsync(
                    normalizedRoot,
                    excludedFolders,
                    writer,
                    directoryDiagnosticSink,
                    resumeAfterRelativeDirectory,
                    cancellationToken),
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

    private static async Task EnumerateFilesAsync(
        string normalizedRoot,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        Func<DiscoveryDirectoryDiagnostic, ValueTask>? directoryDiagnosticSink,
        string? resumeAfterRelativeDirectory,
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

            string relativeDirectory = ComputeRelativeDirectory(dir, normalizedRoot);
            if (!string.IsNullOrWhiteSpace(resumeAfterRelativeDirectory) &&
                string.Compare(
                    relativeDirectory,
                    resumeAfterRelativeDirectory,
                    StringComparison.OrdinalIgnoreCase) <= 0)
            {
                PushSubdirectories(stack, dir);
                continue;
            }

            string canonicalDirectory;
            try
            {
                // Resolve the directory boundary once. Resolving every ordinary
                // file path would repeatedly walk the same parent segments on a
                // large library and defeats bounded scan throughput.
                canonicalDirectory = PathGuard.EnsureWithinRoot(dir, normalizedRoot);
                relativeDirectory = ComputeRelativeDirectory(canonicalDirectory, normalizedRoot);
            }
            catch (PathTraversalException)
            {
                await EmitDirectoryDiagnosticAsync(
                    directoryDiagnosticSink,
                    relativeDirectory,
                    DiscoveryDirectoryStatus.Failed,
                    "directory_outside_root",
                    0).ConfigureAwait(false);
                continue;
            }

            await EmitDirectoryDiagnosticAsync(
                directoryDiagnosticSink,
                relativeDirectory,
                DiscoveryDirectoryStatus.Started,
                null,
                0).ConfigureAwait(false);

            int filesSeen = 0;
            string? errorCode = null;

            // Emit PDF files in this directory.
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(canonicalDirectory, "*.pdf", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                files = [];
                errorCode = "directory_access_denied";
            }
            catch (IOException)
            {
                files = [];
                errorCode = "directory_io_error";
            }

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fullPath = Path.GetFullPath(file);

                // Path-traversal guard: skip anything outside the root.
                string canonicalPath;
                FileInfo info;
                try
                {
                    if (!IsLexicallyWithinRoot(fullPath, normalizedRoot))
                    {
                        continue;
                    }

                    // Read file metadata once for the ordinary path. The prior
                    // implementation queried LinkTarget and then constructed a
                    // second FileInfo for every file, which made large scans
                    // needlessly syscall-heavy. Reparse points still receive
                    // the canonical boundary check before their metadata is read.
                    info = new FileInfo(fullPath);
                    if (!info.Exists)
                    {
                        continue;
                    }

                    canonicalPath = (info.Attributes & FileAttributes.ReparsePoint) == 0
                        ? fullPath
                        : PathGuard.EnsureWithinRoot(fullPath, normalizedRoot);
                    if (!string.Equals(canonicalPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        info = new FileInfo(canonicalPath);
                        if (!info.Exists)
                        {
                            continue;
                        }
                    }
                }
                catch (PathTraversalException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                string relative = ComputeRelativePath(canonicalPath, normalizedRoot);
                var discovered = new DiscoveredFile(
                    AbsolutePath: canonicalPath,
                    RelativePath: relative,
                    SizeBytes: info.Length,
                    MtimeTicks: info.LastWriteTimeUtc.Ticks);

                await writer.WriteAsync(discovered, cancellationToken).ConfigureAwait(false);
                filesSeen++;
            }

            // Push sub-directories onto the stack.
            IReadOnlyList<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(canonicalDirectory)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                subdirectories = [];
                errorCode ??= "subdirectory_access_denied";
            }
            catch (IOException)
            {
                subdirectories = [];
                errorCode ??= "subdirectory_io_error";
            }

            PushSubdirectories(stack, subdirectories);
            await EmitDirectoryDiagnosticAsync(
                directoryDiagnosticSink,
                relativeDirectory,
                errorCode is null ? DiscoveryDirectoryStatus.Completed : DiscoveryDirectoryStatus.Failed,
                errorCode,
                filesSeen).ConfigureAwait(false);
        }
    }

    private static void PushSubdirectories(Stack<string> stack, string directory)
    {
        try
        {
            PushSubdirectories(
                stack,
                Directory.EnumerateDirectories(directory)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
        catch (UnauthorizedAccessException)
        {
            // The caller uses this helper only while skipping directories that
            // are already before a durable resume cursor. An inaccessible branch
            // cannot be resumed from, so it is deliberately skipped; the normal
            // scan path reports the access failure through its diagnostics.
        }
        catch (IOException)
        {
            // See the access-denied note above. Do not let a stale resume cursor
            // turn a recoverable directory error into an unobserved task fault.
        }
    }

    private static void PushSubdirectories(Stack<string> stack, IReadOnlyList<string> directories)
    {
        for (int index = directories.Count - 1; index >= 0; index--)
        {
            stack.Push(directories[index]);
        }
    }

    private static async ValueTask EmitDirectoryDiagnosticAsync(
        Func<DiscoveryDirectoryDiagnostic, ValueTask>? sink,
        string relativeDirectory,
        DiscoveryDirectoryStatus status,
        string? errorCode,
        int filesSeen)
    {
        if (sink is null)
        {
            return;
        }

        await sink(new DiscoveryDirectoryDiagnostic(
            relativeDirectory,
            status,
            errorCode,
            filesSeen,
            DateTimeOffset.UtcNow)).ConfigureAwait(false);
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

    private static string ComputeRelativePath(string absolutePath, string normalizedRoot)
    {
        string root = normalizedRoot + Path.DirectorySeparatorChar;
        if (absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }

        return absolutePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ComputeRelativeDirectory(string absolutePath, string normalizedRoot)
    {
        if (string.Equals(absolutePath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return ComputeRelativePath(absolutePath, normalizedRoot).TrimEnd('/');
    }

    private static bool IsLexicallyWithinRoot(string path, string normalizedRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(normalizedRoot);
        return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
