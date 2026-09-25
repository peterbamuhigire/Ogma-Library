namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Registers a single PDF selected by the user and returns the catalogue book id
/// that can be opened in the reader.
/// </summary>
public interface IDirectPdfOpenService
{
    /// <summary>
    /// Adds or re-matches the supplied PDF file without requiring a whole-folder
    /// scan. Library roots are never changed (Sept-23 Phase 05, K28): a file inside
    /// an enabled root is recorded against that root; any other file becomes a
    /// loose book tracked by its absolute path. A file whose content already exists
    /// in the catalogue becomes another occurrence of that book.
    /// </summary>
    /// <param name="absoluteFilePath">Absolute path to a PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The catalogue book identifier for the selected PDF.</returns>
    Task<string> OpenAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
