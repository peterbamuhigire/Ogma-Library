namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Extracts PDF metadata (Title, Author, Subject, CreationDate) from a PDF file's
/// DocumentInformation dictionary and XMP packet, and persists the results as
/// <c>BookMetadataFields</c> rows with <c>Source = "PDF"</c> (FR-META-001 precursor).
/// </summary>
public interface IMetadataExtractionService
{
    /// <summary>
    /// Extracts metadata from the PDF at <paramref name="absoluteFilePath"/> and upserts
    /// the fields for <paramref name="bookId"/> in the catalogue.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="absoluteFilePath">The absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating whether extraction succeeded and any error message.</returns>
    Task<(bool Success, string? ErrorMessage)> ExtractAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
