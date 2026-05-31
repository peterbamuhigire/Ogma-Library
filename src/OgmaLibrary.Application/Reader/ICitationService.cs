using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Captures citation cards from selected text in the reader (FR-READ-011, V1).
/// </summary>
public interface ICitationService
{
    /// <summary>
    /// Builds a <see cref="CitationCard"/> from the current book metadata, page number,
    /// and the selected text passage.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index of the selection.</param>
    /// <param name="selectedText">The highlighted or selected text passage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The citation card with title, author, page, and selected text.</returns>
    Task<CitationCard> CaptureAsync(
        string bookId,
        int pageIndex,
        string selectedText,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the citation card as plain text to the library sidecar.
    /// </summary>
    /// <param name="card">The citation card to export.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The absolute path of the written citation file.</returns>
    Task<string> ExportAsync(CitationCard card, CancellationToken cancellationToken);
}
