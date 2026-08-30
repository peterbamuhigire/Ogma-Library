namespace OgmaLibrary.Infrastructure.Security;

/// <summary>Raised when a file-system path escapes its declared trust root.</summary>
public sealed class PathTraversalException : IOException
{
    /// <summary>Initializes a new instance of <see cref="PathTraversalException"/>.</summary>
    public PathTraversalException(string path, string root)
        : base($"The path '{path}' is outside the permitted root '{root}'.")
    {
        Path = path;
        Root = root;
    }

    /// <summary>The rejected path.</summary>
    public string Path { get; }

    /// <summary>The permitted root.</summary>
    public string Root { get; }
}

/// <summary>Canonicalizes and bounds file-system paths at I/O trust boundaries.</summary>
public static class PathGuard
{
    /// <summary>
    /// Returns the canonical path when <paramref name="path"/> is inside
    /// <paramref name="root"/>. Existing symbolic links are resolved before the
    /// boundary is checked so a link cannot escape the declared root.
    /// </summary>
    /// <param name="path">The candidate file or directory path.</param>
    /// <param name="root">The permitted root directory.</param>
    /// <returns>The canonical absolute candidate path.</returns>
    /// <exception cref="PathTraversalException">The candidate escapes the root.</exception>
    public static string EnsureWithinRoot(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (path.Contains('\0', StringComparison.Ordinal) || root.Contains('\0', StringComparison.Ordinal))
        {
            throw new PathTraversalException(path, root);
        }

        string canonicalRoot = ResolveExistingSegments(Path.GetFullPath(root));
        string canonicalPath = ResolveExistingSegments(Path.GetFullPath(DecodePath(path)));

        if (!IsWithin(canonicalPath, canonicalRoot))
        {
            throw new PathTraversalException(path, root);
        }

        return canonicalPath;
    }

    /// <summary>Returns an absolute canonical root directory.</summary>
    public static string CanonicalizeRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (root.Contains('\0', StringComparison.Ordinal))
        {
            throw new PathTraversalException(root, root);
        }

        return ResolveExistingSegments(Path.GetFullPath(root));
    }

    private static string DecodePath(string path)
    {
        try
        {
            return Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            throw new PathTraversalException(path, string.Empty);
        }
    }

    private static bool IsWithin(string path, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        if (string.Equals(path, normalizedRoot, Comparison))
        {
            return true;
        }

        return path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, Comparison) ||
               path.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, Comparison);
    }

    private static string ResolveExistingSegments(string fullPath)
    {
        string current = Path.GetPathRoot(fullPath) ?? string.Empty;
        string remainder = fullPath[current.Length..];
        string[] segments = remainder.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            string next = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(next)
                ? new DirectoryInfo(next)
                : new FileInfo(next);

            if (entry.Exists && entry.LinkTarget is not null)
            {
                FileSystemInfo? target = entry.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    current = target.FullName;
                    continue;
                }
            }

            current = next;
        }

        return Path.GetFullPath(current);
    }

    private static StringComparison Comparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
