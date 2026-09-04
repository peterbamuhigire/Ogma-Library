using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Renders the cover thumbnail for a book by rendering page 0 of its PDF with PDFium,
/// resizing to 200×300 px, and writing a JPEG 85% to the sidecar (FR-LIB-005).
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Generates and persists the cover thumbnail for the specified book.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier (used as the content-hash key).</param>
    /// <param name="contentHash">The SHA-256 hex digest of the file (sidecar path key).</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating success and an optional error message.</returns>
    Task<(bool Success, string? ErrorMessage)> GenerateCoverAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>Generates one bounded named cover variant on demand.</summary>
    Task<(bool Success, string? ErrorMessage)> GenerateCoverVariantAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        string variant,
        CancellationToken cancellationToken = default);
}
