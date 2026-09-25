namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Persists and retrieves the library root path and excluded-folder list (FR-LIB-001).
/// Implementations persist to the OS app-data directory across restarts.
/// </summary>
public interface ILibrarySettingsService
{
    /// <summary>
    /// Returns the persisted library root path, or <see langword="null"/> if none is set.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the library root path.
    /// </summary>
    /// <param name="rootPath">The absolute path to the library root folder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of folder names or relative paths to exclude from scans.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the excluded-folder list.
    /// </summary>
    /// <param name="excludedFolders">The new list of excluded folder names or relative paths.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetExcludedFoldersAsync(IReadOnlyList<string> excludedFolders, CancellationToken cancellationToken = default);

    /// <summary>
    /// The outcome of the most recent recovery from an unreadable settings file, or
    /// <see langword="null"/> when the settings have always been readable in this session.
    /// The shell shows it once so a reset is never silent (Sept-23 K95).
    /// </summary>
    LibrarySettingsRecovery? LastRecovery => null;
}

/// <summary>How an unreadable <c>library-settings.json</c> was handled.</summary>
/// <param name="RestoredFromBackup">True when the last good copy was restored; false when defaults were used.</param>
/// <param name="QuarantinedFileName">The file name the damaged copy was kept under for support, if any.</param>
public sealed record LibrarySettingsRecovery(bool RestoredFromBackup, string? QuarantinedFileName);
