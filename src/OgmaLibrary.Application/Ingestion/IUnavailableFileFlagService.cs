namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Flags previously-catalogued files that no longer exist on disk as
/// <c>Unavailable</c>, without deleting any user data (FR-LIB-004).
/// </summary>
public interface IUnavailableFileFlagService
{
    /// <summary>
    /// Iterates all <c>Present</c> BookFiles for the given library root, checks
    /// whether each file still exists, and flags missing files as <c>Missing</c>
    /// with a corresponding <c>AuditEvent</c>.
    /// </summary>
    /// <param name="libraryRoot">The absolute path to the library root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of files flagged as unavailable.</returns>
    Task<int> FlagMissingFilesAsync(string libraryRoot, CancellationToken cancellationToken = default);
}
