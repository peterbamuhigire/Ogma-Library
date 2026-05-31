namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Extracts and caches the text layer for PDF pages using PdfPig, providing the
/// foundation for in-document search (FR-READ-006) and Phase 10 FTS5 extraction.
/// </summary>
public interface ITextLayerService
{
    /// <summary>
    /// Returns the text layer for the specified page. Results are cached in the sidecar
    /// folder (<c>extracted-text/&lt;bookId&gt;/&lt;pageIndex&gt;.json</c>) and
    /// invalidated when the file's content hash changes (Phase 05 identity).
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="ct">A token to cancel the extraction.</param>
    /// <returns>The text layer for the page.</returns>
    Task<TextLayer> ExtractAsync(string bookId, int pageIndex, CancellationToken ct);

    /// <summary>
    /// Returns the extraction quality for a page without loading the full word list.
    /// Useful for search to quickly suppress results from scanned pages (FR-READ-006).
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The extraction quality for the page.</returns>
    Task<ExtractionQuality> GetQualityAsync(string bookId, int pageIndex, CancellationToken ct);
}

/// <summary>
/// Provides in-document text search across all pages of an open PDF (FR-READ-006).
/// Uses cached text layers from <see cref="ITextLayerService"/>.
/// </summary>
public interface IInDocumentSearchService
{
    /// <summary>
    /// Searches all pages of the specified book for the given query string.
    /// Returns matches grouped by page, with bounding-box positions for highlight
    /// overlay rendering (normalized [0.0, 1.0] coordinates).
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="query">The search query (case-insensitive substring match).</param>
    /// <param name="ct">A token to cancel the search.</param>
    /// <returns>Matches ordered by page index.</returns>
    Task<IReadOnlyList<SearchMatch>> SearchAsync(string bookId, string query, CancellationToken ct);
}

/// <summary>
/// A search match within a PDF page (FR-READ-006).
/// </summary>
/// <param name="PageIndex">Zero-based page index containing the match.</param>
/// <param name="ContextSnippet">A short text snippet around the match for the search panel list.</param>
/// <param name="Highlights">
/// Bounding boxes of the matching words on this page, in normalized [0.0, 1.0] coordinates.
/// </param>
public sealed record SearchMatch(
    int PageIndex,
    string ContextSnippet,
    IReadOnlyList<HighlightRect> Highlights);

/// <summary>
/// A normalized highlight rectangle in [0.0, 1.0] page-relative coordinates.
/// Used to position highlight overlays at any zoom level.
/// </summary>
/// <param name="Left">Left edge in [0.0, 1.0].</param>
/// <param name="Top">Top edge in [0.0, 1.0].</param>
/// <param name="Right">Right edge in [0.0, 1.0].</param>
/// <param name="Bottom">Bottom edge in [0.0, 1.0].</param>
public sealed record HighlightRect(double Left, double Top, double Right, double Bottom);
