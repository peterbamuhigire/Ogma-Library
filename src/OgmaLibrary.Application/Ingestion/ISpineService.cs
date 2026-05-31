namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Renders the spine strip for a book by rendering page 0 of its PDF with PDFium,
/// cropping and scaling to 7×100 px, and writing a JPEG to the sidecar (FR-LIB-005).
/// </summary>
public interface ISpineService
{
    /// <summary>
    /// Generates and persists the spine strip for the specified book.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file.</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> GenerateSpineAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
