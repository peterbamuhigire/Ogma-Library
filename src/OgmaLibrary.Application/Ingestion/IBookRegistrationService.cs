namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Registers a newly-discovered PDF file in the catalogue and enqueues the background
/// asset-generation jobs (FR-LIB-003, NFR-OGMA-009).
/// </summary>
public interface IBookRegistrationService
{
    /// <summary>
    /// Inserts a new <c>Book</c> and <c>BookFile</c> row for the discovered file,
    /// then enqueues metadata extraction and thumbnail generation jobs.
    /// </summary>
    /// <param name="discovered">The discovered file record.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file content.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stable book identifier assigned to the new book.</returns>
    Task<string> RegisterAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <c>BookFile</c> for a previously-catalogued book whose path changed
    /// (i.e., file was renamed or moved). Re-activates the file if it was Missing.
    /// </summary>
    /// <param name="bookId">The existing book identifier.</param>
    /// <param name="discovered">The file at its new path.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file content.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateFilePathAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default);
}
