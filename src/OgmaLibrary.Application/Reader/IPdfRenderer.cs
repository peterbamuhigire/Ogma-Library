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
    /// Gets the effective page geometry used by the reader layout and overlay
    /// coordinate transform. Existing test doubles use the safe fallback.
    /// </summary>
    PdfPageGeometry GetPageGeometry(int pageIndex)
    {
        int rotation = GetPageRotationDegrees(pageIndex);
        return PdfPageGeometry.Fallback(pageIndex, rotation);
    }

    /// <summary>The versioned reader capability profile for this processor.</summary>
    PdfCapabilityProfile CapabilityProfile => PdfCapabilityProfile.Current;

    /// <summary>Reads bounded document information and optional XMP metadata.</summary>
    PdfDocumentMetadata ReadDocumentMetadata() => new();

    /// <summary>Reads bounded, sanitized outline targets from the document.</summary>
    IReadOnlyList<PdfOutlineEntry> ReadOutline() => [];

    /// <summary>
    /// Synchronously extracts the text layer for a single page using PdfPig.
    /// Returns an empty layer (with <see cref="ExtractionQuality.Scanned"/>) for
    /// pages that contain no extractable text.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>The extracted text layer.</returns>
    TextLayer ExtractTextLayer(int pageIndex);

    /// <summary>
    /// Asynchronously returns the PDF-standard clockwise rotation for a page. Reader and
    /// UI callers must use the asynchronous members so no worker IPC blocks the UI thread
    /// (Sept-23 Kaizen K32). Test doubles inherit a synchronous fallback.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The page rotation in degrees.</returns>
    Task<int> GetPageRotationDegreesAsync(int pageIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(GetPageRotationDegrees(pageIndex));
    }

    /// <summary>Asynchronously gets the effective page geometry.</summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The page geometry, or a safe fallback.</returns>
    Task<PdfPageGeometry> GetPageGeometryAsync(int pageIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(GetPageGeometry(pageIndex));
    }

    /// <summary>Asynchronously reads bounded document information.</summary>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The document metadata.</returns>
    Task<PdfDocumentMetadata> ReadDocumentMetadataAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ReadDocumentMetadata());
    }

    /// <summary>Asynchronously reads bounded, sanitized outline targets.</summary>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The outline entries.</returns>
    Task<IReadOnlyList<PdfOutlineEntry>> ReadOutlineAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ReadOutline());
    }

    /// <summary>Asynchronously extracts the text layer for a single page.</summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="ct">A token to cancel the request while it is queued.</param>
    /// <returns>The extracted text layer.</returns>
    Task<TextLayer> ExtractTextLayerAsync(int pageIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ExtractTextLayer(pageIndex));
    }
}

/// <summary>
/// Optional health surface of a supervised PDF renderer. The reader forwards recovery
/// notifications as <see cref="ReaderEvent.EngineRecovered"/> events so diagnostics and
/// logging can observe worker respawns without depending on Infrastructure.
/// </summary>
public interface IPdfRendererHealth
{
    /// <summary>Raised after the renderer transparently replaced a lost worker session.</summary>
    event EventHandler<PdfRendererRecoveredEventArgs>? Recovered;

    /// <summary>Gets a bounded, content-free snapshot of renderer health counters.</summary>
    /// <returns>The current health counters.</returns>
    PdfRendererHealthSnapshot GetHealthSnapshot();
}

/// <summary>Describes one transparent worker-session recovery.</summary>
/// <param name="Reason">A stable, content-free reason code such as <c>worker_exited</c>.</param>
/// <param name="RespawnCount">The number of recoveries in this renderer's lifetime.</param>
/// <param name="Elapsed">Time taken to start the replacement worker.</param>
public sealed class PdfRendererRecoveredEventArgs(string Reason, int RespawnCount, TimeSpan Elapsed) : EventArgs
{
    /// <summary>The stable reason code (never a path or document content).</summary>
    public string Reason { get; } = Reason;

    /// <summary>The number of recoveries so far.</summary>
    public int RespawnCount { get; } = RespawnCount;

    /// <summary>Time taken to start the replacement worker.</summary>
    public TimeSpan Elapsed { get; } = Elapsed;
}

/// <summary>Content-free health counters for a supervised renderer.</summary>
/// <param name="State">Healthy, Idle, Respawning or Failed.</param>
/// <param name="RespawnCount">Recoveries after worker loss.</param>
/// <param name="IdleCloseCount">Workers closed by the idle timeout.</param>
/// <param name="QueueDepth">Requests waiting for the worker.</param>
/// <param name="DiscardedResponses">Late responses discarded after a timeout or cancellation.</param>
/// <param name="StandardErrorLines">Worker diagnostic lines drained from standard error.</param>
public sealed record PdfRendererHealthSnapshot(
    string State,
    int RespawnCount,
    int IdleCloseCount,
    int QueueDepth,
    long DiscardedResponses,
    long StandardErrorLines);

/// <summary>
/// Thrown when the isolated reader worker could not be recovered within the respawn
/// budget. The message is a stable, content-free diagnostic; callers show a localized
/// message and offer to reopen the book.
/// </summary>
public sealed class PdfRendererUnavailableException : Exception
{
    /// <summary>Initializes the exception.</summary>
    public PdfRendererUnavailableException()
        : base("The PDF reader engine is unavailable.")
    {
    }

    /// <summary>Initializes the exception with a diagnostic message.</summary>
    /// <param name="message">The content-free diagnostic message.</param>
    public PdfRendererUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a diagnostic message and cause.</summary>
    /// <param name="message">The content-free diagnostic message.</param>
    /// <param name="innerException">The last worker failure.</param>
    public PdfRendererUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
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
    bool IsLowResPreview = false)
{
    /// <summary>Page box used when the renderer supports explicit page boxes.</summary>
    public PdfPageBox PageBox { get; init; } = PdfPageBox.CropBox;

    /// <summary>Whether annotation appearance streams are included in the raster.</summary>
    public PdfAnnotationRenderMode AnnotationMode { get; init; } = PdfAnnotationRenderMode.Exclude;

    /// <summary>Whether current form values are painted into the raster.</summary>
    public bool IncludeFormValues { get; init; }

    /// <summary>Optional-content policy for this render.</summary>
    public PdfOptionalContentMode OptionalContentMode { get; init; } = PdfOptionalContentMode.Default;

    /// <summary>Explicit page rotation override; null means use the PDF page rotation.</summary>
    public int? RotationDegrees { get; init; }

    /// <summary>Stable cache identity for all visual inputs.</summary>
    public string CacheFingerprint => string.Join(
        "|",
        WidthPx,
        HeightPx,
        Scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        IsLowResPreview,
        PageBox,
        AnnotationMode,
        IncludeFormValues,
        OptionalContentMode,
        RotationDegrees?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "pdf");
}

/// <summary>Page box selection for a render request.</summary>
public enum PdfPageBox
{
    /// <summary>The effective crop box, which is the normal display box.</summary>
    CropBox = 0,

    /// <summary>The physical media box when a print-oriented view is needed.</summary>
    MediaBox = 1,
}

/// <summary>Controls whether annotation appearance streams are painted.</summary>
public enum PdfAnnotationRenderMode
{
    /// <summary>Do not paint annotations.</summary>
    Exclude = 0,

    /// <summary>Paint safe appearance streams without executing actions.</summary>
    AppearanceOnly = 1,
}

/// <summary>Controls optional-content visibility for a page render.</summary>
public enum PdfOptionalContentMode
{
    /// <summary>Use the document/default configuration.</summary>
    Default = 0,

    /// <summary>Use only content explicitly known to be visible.</summary>
    VisibleOnly = 1,
}

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
/// Effective geometry for one physical PDF page. Width and height are the
/// unrotated PDF user-space dimensions; display dimensions include page rotation.
/// </summary>
public sealed record PdfPageGeometry(
    int PageIndex,
    double WidthPoints,
    double HeightPoints,
    int RotationDegrees,
    bool IsFallback = false)
{
    /// <summary>Width after applying the page rotation for screen display.</summary>
    public double DisplayWidthPoints => RotationDegrees is 90 or 270 ? HeightPoints : WidthPoints;

    /// <summary>Height after applying the page rotation for screen display.</summary>
    public double DisplayHeightPoints => RotationDegrees is 90 or 270 ? WidthPoints : HeightPoints;

    /// <summary>Creates a safe A4-shaped fallback when geometry is unavailable.</summary>
    public static PdfPageGeometry Fallback(int pageIndex, int rotationDegrees = 0) =>
        new(pageIndex, 595, 842, NormalizeRotation(rotationDegrees), IsFallback: true);

    private static int NormalizeRotation(int value) => ((value % 360) + 360) % 360;
}

/// <summary>Bounded PDF document information exposed to metadata consumers.</summary>
public sealed record PdfDocumentMetadata(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    string? Creator = null,
    string? XmpXml = null);

/// <summary>Sanitized outline entry with a physical zero-based page target.</summary>
public sealed record PdfOutlineEntry(string Title, int PageIndex, int Level);

/// <summary>Support status for a PDF processor capability in the published profile.</summary>
public enum PdfFeatureSupportStatus
{
    /// <summary>The capability is implemented and covered by the profile.</summary>
    Supported = 0,

    /// <summary>The capability is usable with documented limitations.</summary>
    Degraded = 1,

    /// <summary>The capability is intentionally blocked by reader policy.</summary>
    Refused = 2,

    /// <summary>The capability has not yet received release evidence.</summary>
    NotAssessed = 3,
}

/// <summary>
/// Versioned, deliberately bounded reader capability profile. This is a product
/// contract, not a claim that every PDF 2.0 feature is implemented.
/// </summary>
public sealed record PdfCapabilityProfile(
    string ProfileId,
    string PdfStandard,
    string EngineFamily,
    string ProfileVersion,
    IReadOnlyDictionary<string, PdfFeatureSupportStatus> Features)
{
    /// <summary>Current Ogma baseline profile used until a release profile is promoted.</summary>
    public static PdfCapabilityProfile Current { get; } = new(
        "ogma-reader-baseline",
        "PDF 2.0 processor subset",
        "PDFium + PdfPig",
        "1",
        new Dictionary<string, PdfFeatureSupportStatus>(StringComparer.Ordinal)
        {
            ["page-rendering"] = PdfFeatureSupportStatus.Supported,
            ["page-geometry"] = PdfFeatureSupportStatus.Degraded,
            ["text-extraction"] = PdfFeatureSupportStatus.Degraded,
            ["outlines"] = PdfFeatureSupportStatus.Degraded,
            ["annotations"] = PdfFeatureSupportStatus.Degraded,
            ["forms"] = PdfFeatureSupportStatus.Refused,
            ["javascript-and-launch-actions"] = PdfFeatureSupportStatus.Refused,
            ["tagged-pdf-semantics"] = PdfFeatureSupportStatus.NotAssessed,
            ["pdf-ua-2-input"] = PdfFeatureSupportStatus.NotAssessed,
            ["pdf-a-4-input"] = PdfFeatureSupportStatus.NotAssessed,
        });
}

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
