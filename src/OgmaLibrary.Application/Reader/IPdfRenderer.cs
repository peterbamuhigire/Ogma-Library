namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Renders pages of a single PDF file to bitmaps and extracts text layers.
/// Implementations wrap PDFium via PDFtoImage (ADR-0004); callers depend only
/// on this interface so the native library stays behind the adapter boundary.
/// </summary>
/// <remarks>
/// Instances are document-scoped (one per open file). Callers must dispose
/// the renderer when the document is closed to release the native handle.
/// The implementation must be thread-safe for concurrent render calls.
/// </remarks>
public interface IPdfRenderer : IDisposable
{
    /// <summary>The total number of pages in the document.</summary>
    int PageCount { get; }

    /// <summary>
    /// Renders a single page to a PNG byte array at the requested size.
    /// The render is dispatched off the calling thread (Task.Run); the caller
    /// must not block the UI thread waiting for the result.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="request">Render parameters (size, scale, rotation).</param>
    /// <param name="ct">A token to cancel the render.</param>
    /// <returns>The render result containing PNG bytes and page dimensions.</returns>
    Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct);

    /// <summary>
    /// Returns the PDF-standard clockwise rotation for a page: 0, 90, 180, or 270 degrees.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>The page rotation in degrees.</returns>
    int GetPageRotationDegrees(int pageIndex);

    /// <summary>
    /// Synchronously extracts the text layer for a single page using PdfPig.
    /// Returns an empty layer (with <see cref="ExtractionQuality.Scanned"/>) for
    /// pages that contain no extractable text.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>The extracted text layer.</returns>
    TextLayer ExtractTextLayer(int pageIndex);
}

/// <summary>
/// Parameters for a single-page render request (NFR-OGMA-005 page-turn budget).
/// </summary>
/// <param name="WidthPx">The desired output width in pixels.</param>
/// <param name="HeightPx">The desired output height in pixels (0 = auto from aspect ratio).</param>
/// <param name="Scale">Additional DPI scale factor (1.0 = 96 dpi).</param>
/// <param name="IsLowResPreview">
/// When <see langword="true"/> the render is a low-resolution preview (quarter scale)
/// used for instant visual feedback before the full-res render completes.
/// </param>
public sealed record RenderRequest(
    int WidthPx,
    int HeightPx = 0,
    double Scale = 1.0,
    bool IsLowResPreview = false);

/// <summary>
/// The result of a single-page render.
/// </summary>
/// <param name="PngBytes">The rendered page as a PNG byte array.</param>
/// <param name="PageWidthPoints">The page width in PDF points (1/72 inch).</param>
/// <param name="PageHeightPoints">The page height in PDF points.</param>
/// <param name="PageIndex">The zero-based page index that was rendered.</param>
public sealed record RenderResult(
    byte[] PngBytes,
    double PageWidthPoints,
    double PageHeightPoints,
    int PageIndex);

/// <summary>
/// Indicates the quality of text extracted from a PDF page by the text-layer service.
/// Used by Phase 10 FTS5 extraction to prioritize pages (SOURCE-SUMMARY §D READ).
/// </summary>
public enum ExtractionQuality
{
    /// <summary>Full selectable text layer extracted successfully.</summary>
    Full = 0,

    /// <summary>Text extracted but with low confidence or partial coverage.</summary>
    Partial = 1,

    /// <summary>No text layer found on this page.</summary>
    Empty = 2,

    /// <summary>
    /// Page appears to be a scanned image with no embedded text.
    /// In-document search is suppressed for this page; OCR is deferred to Phase 15.
    /// </summary>
    Scanned = 3,
}

/// <summary>
/// The text layer extracted from a single PDF page via PdfPig (FR-READ-006).
/// Bounding boxes are normalized to [0.0, 1.0] relative to page dimensions.
/// </summary>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="Words">The extracted words with their bounding boxes.</param>
/// <param name="Quality">The extraction quality for this page.</param>
public sealed record TextLayer(
    int PageIndex,
    IReadOnlyList<TextWord> Words,
    ExtractionQuality Quality);

/// <summary>
/// A single word extracted from the text layer with its normalized bounding box.
/// </summary>
/// <param name="Text">The word text.</param>
/// <param name="Left">Left edge in [0.0, 1.0] (relative to page width).</param>
/// <param name="Top">Top edge in [0.0, 1.0] (relative to page height).</param>
/// <param name="Right">Right edge in [0.0, 1.0].</param>
/// <param name="Bottom">Bottom edge in [0.0, 1.0].</param>
public sealed record TextWord(
    string Text,
    double Left,
    double Top,
    double Right,
    double Bottom);
