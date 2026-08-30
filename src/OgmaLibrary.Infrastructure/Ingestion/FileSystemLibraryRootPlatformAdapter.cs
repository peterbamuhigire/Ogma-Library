using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Default filesystem adapter for Windows and macOS. It deliberately performs
/// only a bounded root probe; recursive enumeration belongs to scan workers.
/// </summary>
public sealed class FileSystemLibraryRootPlatformAdapter : ILibraryRootPlatformAdapter
{
    /// <inheritdoc />
    public string CanonicalizeRoot(string path) => PathGuard.CanonicalizeRoot(path);

    /// <inheritdoc />
    public LibraryRootProbeResult Probe(string canonicalLocator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalLocator);

        try
        {
            if (!Directory.Exists(canonicalLocator))
            {
                return new LibraryRootProbeResult(
                    LibraryRootStatus.Unavailable,
                    LibraryRootPermissionStatus.Unknown,
                    GetVolumeIdentity(canonicalLocator));
            }

            // Opening the directory and reading at most one entry tests access
            // without enumerating an entire external volume.
            using IEnumerator<string> probe = Directory
                .EnumerateFileSystemEntries(canonicalLocator)
                .Take(1)
                .GetEnumerator();
            _ = probe.MoveNext();

            return new LibraryRootProbeResult(
                LibraryRootStatus.Available,
                LibraryRootPermissionStatus.Granted,
                GetVolumeIdentity(canonicalLocator));
        }
        catch (UnauthorizedAccessException)
        {
            return new LibraryRootProbeResult(
                LibraryRootStatus.PermissionDenied,
                LibraryRootPermissionStatus.Denied,
                GetVolumeIdentity(canonicalLocator));
        }
        catch (IOException)
        {
            return new LibraryRootProbeResult(
                LibraryRootStatus.Unavailable,
                LibraryRootPermissionStatus.Unknown,
                GetVolumeIdentity(canonicalLocator));
        }
        catch (ArgumentException)
        {
            return new LibraryRootProbeResult(
                LibraryRootStatus.Unavailable,
                LibraryRootPermissionStatus.Unknown,
                null);
        }
    }

    private static string? GetVolumeIdentity(string canonicalLocator)
    {
        string? pathRoot = Path.GetPathRoot(canonicalLocator);
        return string.IsNullOrWhiteSpace(pathRoot)
            ? null
            : pathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
