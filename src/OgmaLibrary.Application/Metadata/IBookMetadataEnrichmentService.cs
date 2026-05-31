namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// Runs the deterministic, no-AI metadata enrichment flow for one catalogue book:
/// derive search keys, query public metadata providers, merge results, apply high
/// confidence fields, and optionally write supported fields back into the PDF.
/// </summary>
public interface IBookMetadataEnrichmentService
{
    /// <summary>
    /// Enriches one book and returns a success flag plus an optional failure message.
    /// The implementation must not call any AI service or token-consuming endpoint.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="absoluteFilePath">The absolute PDF path, when already known.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<(bool Success, string? ErrorMessage)> EnrichAsync(
        string bookId,
        string? absoluteFilePath,
        CancellationToken cancellationToken = default);
}
