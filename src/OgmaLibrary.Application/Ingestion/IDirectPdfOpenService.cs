namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Registers a single PDF selected by the user and returns the catalogue book id
/// that can be opened in the reader.
/// </summary>
public interface IDirectPdfOpenService
{
    /// <summary>
    /// Adds or re-matches the supplied PDF file without requiring a whole-folder
    /// scan. The containing folder becomes the active library root for reader
    /// resolution of the selected file.
    /// </summary>
    /// <param name="absoluteFilePath">Absolute path to a PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The catalogue book identifier for the selected PDF.</returns>
    Task<string> OpenAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
