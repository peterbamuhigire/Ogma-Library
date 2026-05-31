using OgmaLibrary.Application.Reader;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Production <see cref="IPdfRenderer"/> that wraps PDFtoImage (for page rendering)
/// and PdfPig (for text-layer extraction). Selected as the winning wrapper in the
/// Phase 01 spike benchmark (ADR-0004 amendment, 2026-05-30).
/// </summary>
/// <remarks>
/// Thread-safety: <see cref="RenderPageAsync"/> dispatches work on the thread pool
/// and is safe for concurrent calls. <see cref="ExtractTextLayer"/> is synchronous
/// but thread-safe because PdfPig opens a fresh document stream per call.
/// The native PDFium library is loaded once by PDFtoImage; this adapter holds no
/// persistent native handle.
/// </remarks>
public sealed class PdfiumAdapter : IPdfRenderer
{
    private readonly string _filePath;
    private readonly byte[] _fileBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new adapter for the specified PDF file.
    /// </summary>
    /// <param name="filePath">The absolute path to the PDF file.</param>
    public PdfiumAdapter(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _fileBytes = File.ReadAllBytes(filePath);
        PageCount = DetectPageCount();
    }

    /// <inheritdoc />
    public int PageCount { get; }

    /// <inheritdoc />
    public async Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);

        ct.ThrowIfCancellationRequested();

        // Calculate output width; apply low-res preview downscale if requested.
        double scale = request.IsLowResPreview ? request.Scale * 0.25 : request.Scale;
        int targetWidth = request.IsLowResPreview
            ? Math.Max(1, request.WidthPx / 4)
            : Math.Max(1, request.WidthPx);

        // Render on a thread pool thread — never block the UI thread (NFR-PROD-005).
        byte[] pngBytes = await Task.Run(() => DoRender(pageIndex, targetWidth, scale, ct), ct)
            .ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        // Extract page dimensions from PdfPig for aspect-ratio calculations.
        (double widthPts, double heightPts) = GetPageDimensions(pageIndex);

        return new RenderResult(pngBytes, widthPts, heightPts, pageIndex);
    }

    /// <inheritdoc />
    public TextLayer ExtractTextLayer(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);

        try
        {
            using var doc = PdfDocument.Open(_fileBytes);
            var pages = doc.GetPages().ToList();
            if (pageIndex >= pages.Count)
            {
                return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
            }

            var page = pages[pageIndex];
            double pageWidth = page.Width;
            double pageHeight = page.Height;

            if (pageWidth <= 0 || pageHeight <= 0)
            {
                return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
            }

            var pdfPigWords = page.GetWords().ToList();

            if (pdfPigWords.Count == 0)
            {
                // Heuristic: if there are images but no words, it is probably scanned.
                bool hasImages = page.GetImages().Any();
                ExtractionQuality quality = hasImages ? ExtractionQuality.Scanned : ExtractionQuality.Empty;
                return new TextLayer(pageIndex, [], quality);
            }

            var words = new List<TextWord>(pdfPigWords.Count);
            foreach (var w in pdfPigWords)
            {
                var bb = w.BoundingBox;

                // PdfPig uses bottom-left origin; normalize to top-left [0,1].
                double left = bb.Left / pageWidth;
                double bottom = bb.Bottom / pageHeight;
                double right = bb.Right / pageWidth;
                double top = bb.Top / pageHeight;

                // Convert from bottom-left origin to top-left origin.
                double normTop = 1.0 - top;
                double normBottom = 1.0 - bottom;

                words.Add(new TextWord(w.Text, left, normTop, right, normBottom));
            }

            ExtractionQuality extractionQuality = pdfPigWords.Count > 5
                ? ExtractionQuality.Full
                : ExtractionQuality.Partial;

            return new TextLayer(pageIndex, words, extractionQuality);
        }
        catch (Exception)
        {
            return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    private byte[] DoRender(int pageIndex, int targetWidth, double scale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // PDFtoImage renders to an SKBitmap; we encode it to PNG bytes.
        using var stream = new MemoryStream(_fileBytes);
        var options = new RenderOptions(
            Dpi: (int)(72 * scale),
            Width: targetWidth,
            Height: null,
            WithAnnotations: false,
            WithFormFill: false,
            WithAspectRatio: true,
            Rotation: PdfRotation.Rotate0,
            AntiAliasing: PdfAntiAliasing.All,
            BackgroundColor: null,
            Bounds: null,
            UseTiling: false,
            DpiRelativeToBounds: false,
            Grayscale: false);

        // PDFtoImage is only supported on Windows, macOS, and Linux (the supported
        // Ogma platforms per ADR-0004). CA1416 is suppressed because all three are
        // in the supported set; other platforms are excluded by packaging.
#pragma warning disable CA1416
        using SKBitmap bitmap = PDFtoImage.Conversion.ToImage(stream, options: options, page: pageIndex);
#pragma warning restore CA1416

        ct.ThrowIfCancellationRequested();

        using var pngStream = new MemoryStream();
        bitmap.Encode(pngStream, SKEncodedImageFormat.Png, 100);
        return pngStream.ToArray();
    }

    private int DetectPageCount()
    {
        try
        {
            using var doc = PdfDocument.Open(_fileBytes);
            return doc.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }

    private (double Width, double Height) GetPageDimensions(int pageIndex)
    {
        try
        {
            using var doc = PdfDocument.Open(_fileBytes);
            var pages = doc.GetPages().ToList();
            if (pageIndex >= pages.Count)
            {
                return (595, 842); // A4 fallback
            }

            var page = pages[pageIndex];
            return (page.Width, page.Height);
        }
        catch
        {
            return (595, 842);
        }
    }
}
