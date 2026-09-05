using OgmaLibrary.Application.Reader;
using PDFtoImage;
using PDFtoImage.Exceptions;
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
/// but thread-safe because each adapter owns a document-scoped PdfPig parse.
/// The native PDFium library is loaded once by PDFtoImage; this adapter holds no
/// persistent native handle.
/// </remarks>
public sealed class PdfiumAdapter : IPdfRenderer
{
    private const int MaxWordsPerPage = 100_000;
    private const int MaxWordLength = 4_096;
    private const int MaxEmbeddedImageCount = 32;
    private const int MaxEmbeddedImageDimension = 8_192;
    private const int MaxEmbeddedImageBytes = 16 * 1024 * 1024;
    private readonly string _filePath;
    private readonly byte[] _fileBytes;
    private readonly char[]? _password;
    private readonly Lazy<IReadOnlyList<PageInfo>> _pageInfo;
    private readonly Lazy<PdfDocument> _textDocument;
    private readonly object _textExtractionGate = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new adapter for the specified PDF file.
    /// </summary>
    /// <param name="filePath">The absolute path to the PDF file.</param>
    public PdfiumAdapter(string filePath)
        : this(filePath, password: null, copyPassword: false)
    {
    }

    /// <summary>
    /// Initializes a new adapter for the specified password-protected PDF file.
    /// </summary>
    /// <param name="filePath">The absolute path to the PDF file.</param>
    /// <param name="password">The password characters for this document session.</param>
    public PdfiumAdapter(string filePath, char[] password)
        : this(filePath, password ?? throw new ArgumentNullException(nameof(password)), copyPassword: true)
    {
    }

    private PdfiumAdapter(string filePath, char[]? password, bool copyPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _fileBytes = File.ReadAllBytes(filePath);
        _password = password is null ? null : copyPassword ? password.ToArray() : password;
        _pageInfo = new Lazy<IReadOnlyList<PageInfo>>(
            ReadPageInfo,
            LazyThreadSafetyMode.ExecutionAndPublication);
        _textDocument = new Lazy<PdfDocument>(
            OpenPdfPigDocument,
            LazyThreadSafetyMode.ExecutionAndPublication);
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
    public int GetPageRotationDegrees(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);

        try
        {
            IReadOnlyList<PageInfo> pages = _pageInfo.Value;
            if (pageIndex >= pages.Count)
            {
                return 0;
            }

            return pages[pageIndex].Rotation;
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public TextLayer ExtractTextLayer(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);

        try
        {
            lock (_textExtractionGate)
            {
                // Resolve one page on demand. Materialising every PdfPig page
                // retains the complete document object graph and caused large
                // real-PDF corpora to grow into multi-gigabyte working sets.
                Page page = _textDocument.Value.GetPage(pageIndex + 1);

                double pageWidth = page.Width;
                double pageHeight = page.Height;

                if (pageWidth <= 0 || pageHeight <= 0)
                {
                    return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
                }

                var words = new List<TextWord>();
                bool truncated = false;
                foreach (var pdfPigWord in page.GetWords())
                {
                    if (words.Count >= MaxWordsPerPage)
                    {
                        truncated = true;
                        break;
                    }

                    string text = pdfPigWord.Text.Normalize(System.Text.NormalizationForm.FormC);
                    if (text.Length > MaxWordLength)
                    {
                        text = text[..MaxWordLength];
                        truncated = true;
                    }

                    var bb = pdfPigWord.BoundingBox;
                    double left = Math.Clamp(bb.Left / pageWidth, 0.0, 1.0);
                    double bottom = Math.Clamp(bb.Bottom / pageHeight, 0.0, 1.0);
                    double right = Math.Clamp(bb.Right / pageWidth, left, 1.0);
                    double top = Math.Clamp(bb.Top / pageHeight, bottom, 1.0);

                    // PdfPig uses bottom-left origin; normalize to top-left [0,1].
                    words.Add(new TextWord(text, left, 1.0 - top, right, 1.0 - bottom));
                }

                if (words.Count == 0)
                {
                    // Heuristic: if there are images but no words, it is probably scanned.
                    bool hasImages = page.GetImages().Any();
                    ExtractionQuality quality = hasImages ? ExtractionQuality.Scanned : ExtractionQuality.Empty;
                    return new TextLayer(pageIndex, [], quality);
                }

                ExtractionQuality extractionQuality = truncated
                    ? ExtractionQuality.Partial
                    : words.Count > 5
                    ? ExtractionQuality.Full
                    : ExtractionQuality.Partial;

                return new TextLayer(pageIndex, words, extractionQuality);
            }
        }
        catch (Exception)
        {
            return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_password is not null)
        {
            Array.Clear(_password);
        }

        if (_textDocument.IsValueCreated)
        {
            try
            {
                _textDocument.Value.Dispose();
            }
            catch
            {
                // Disposal must not hide the original malformed-PDF failure.
            }
        }

        _disposed = true;
    }

    private byte[] DoRender(int pageIndex, int targetWidth, double scale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // PDFtoImage renders to an SKBitmap; we encode it to PNG bytes.
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
        using SKBitmap bitmap = Conversion.ToImage(
            _fileBytes,
            page: pageIndex,
            password: CreatePasswordString(),
            options: options);
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
#pragma warning disable CA1416
            return Conversion.GetPageCount(_fileBytes, CreatePasswordString());
#pragma warning restore CA1416
        }
        catch (PdfPasswordProtectedException) when (_password is null)
        {
            throw new PdfPasswordRequiredException(_filePath);
        }
        catch (PdfPasswordProtectedException)
        {
            throw new PdfPasswordIncorrectException(_filePath);
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
            IReadOnlyList<PageInfo> pages = _pageInfo.Value;
            if (pageIndex >= pages.Count)
            {
                return (595, 842); // A4 fallback
            }

            PageInfo page = pages[pageIndex];
            return (page.Width, page.Height);
        }
        catch
        {
            return (595, 842);
        }
    }

    private static int NormalizeRotation(int rotation) =>
        ((rotation % 360) + 360) % 360;

    private List<PageInfo> ReadPageInfo()
    {
        try
        {
            using var doc = OpenPdfPigDocument();
            return doc.GetPages()
                .Select(page => new PageInfo(
                    page.Width,
                    page.Height,
                    NormalizeRotation(page.Rotation.Value)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Extracts one bounded, decodable image from the first PDF page for use as
    /// an embedded-cover candidate. The image is returned as normalized PNG
    /// bytes so callers never need to interpret PDF image filters or color
    /// spaces. Only the first page and the first bounded set of image objects
    /// are inspected; failure is represented by <see langword="null"/> so the
    /// caller can apply the deterministic first-page fallback.
    /// </summary>
    public byte[]? TryExtractEmbeddedCoverImage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            lock (_textExtractionGate)
            {
                Page page = _textDocument.Value.GetPage(1);
                IEnumerable<IPdfImage> candidates = page.GetImages()
                    .Take(MaxEmbeddedImageCount)
                    .OrderByDescending(image =>
                        (long)Math.Max(0, image.WidthInSamples) * Math.Max(0, image.HeightInSamples));

                foreach (IPdfImage candidate in candidates)
                {
                    if (candidate.IsImageMask ||
                        candidate.WidthInSamples is <= 0 or > MaxEmbeddedImageDimension ||
                        candidate.HeightInSamples is <= 0 or > MaxEmbeddedImageDimension)
                    {
                        continue;
                    }

                    ReadOnlyMemory<byte> raw = candidate.RawMemory;
                    if (raw.Length == 0 || raw.Length > MaxEmbeddedImageBytes)
                    {
                        continue;
                    }

                    byte[]? png;
#pragma warning disable CS8600 // PdfPig's out annotation permits a null result when conversion is unavailable.
                    bool convertedToPng = candidate.TryGetPng(out png);
#pragma warning restore CS8600
                    byte[] encoded = convertedToPng && png is not null
                        ? png
                        : raw.ToArray();
                    using SKBitmap? bitmap = SKBitmap.Decode(encoded);
                    if (bitmap is null ||
                        bitmap.Width is <= 0 or > MaxEmbeddedImageDimension ||
                        bitmap.Height is <= 0 or > MaxEmbeddedImageDimension)
                    {
                        continue;
                    }

                    using SKImage image = SKImage.FromBitmap(bitmap);
                    using SKData normalized = image.Encode(SKEncodedImageFormat.Png, 100);
                    if (normalized.Size is <= 0 or > MaxEmbeddedImageBytes)
                    {
                        continue;
                    }

                    return normalized.ToArray();
                }
            }
        }
        catch (Exception)
        {
            // Embedded art is an optional source. A malformed or unsupported
            // image must fall through to generated first-page art.
        }

        return null;
    }

    private PdfDocument OpenPdfPigDocument()
    {
        var options = new ParsingOptions { UseLenientParsing = true };
        string? password = CreatePasswordString();
        if (password is not null)
        {
            options.Password = password;
        }

        return PdfDocument.Open(_fileBytes, options);
    }

    private string? CreatePasswordString() =>
        _password is null ? null : new string(_password);

    private readonly record struct PageInfo(double Width, double Height, int Rotation);
}
