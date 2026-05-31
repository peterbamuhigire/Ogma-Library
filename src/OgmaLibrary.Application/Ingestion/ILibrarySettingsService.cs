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
}
