using System.Runtime.Versioning;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using PDFtoImage;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Renders page 0 of a PDF with PDFtoImage (spike S02 winner, v5.x for SkiaSharp 3.x),
/// resizes to 200×300 px via SkiaSharp, and writes a JPEG 85% to the sidecar
/// (FR-LIB-005). Always runs off the UI thread via <c>Task.Run</c>.
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private const int TargetWidth = 200;
    private const int TargetHeight = 300;

    private readonly ISidecarService _sidecar;

    /// <summary>
    /// Initializes a new instance of <see cref="ThumbnailService"/>.
    /// </summary>
    /// <param name="sidecar">The sidecar service used to resolve output paths.</param>
    public ThumbnailService(ISidecarService sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        _sidecar = sidecar;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> GenerateCoverAsync(
        string bookId,
        string contentHash,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        try
        {
            string outputPath = _sidecar.Resolve(contentHash, SidecarClass.Covers);

            await Task.Run(() =>
            {
                RenderAndSaveCover(absoluteFilePath, outputPath);
            }, cancellationToken).ConfigureAwait(false);

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    // PDFtoImage is supported on Windows, macOS, Linux (the Ogma MVP platforms).
    // The SupportedOSPlatform attribute on Conversion.ToImage is an API marker;
    // the actual Ogma app targets only Windows and macOS.
#pragma warning disable CA1416 // Platform compatibility
    private static void RenderAndSaveCover(string pdfPath, string outputPath)
    {
        // Render page 0 to a temporary JPEG file then resize with SkiaSharp.
        // PDFtoImage 5.x: ToImage(Stream, System.Index, bool leaveOpen, string? password, RenderOptions).
        using var pdfStream = File.OpenRead(pdfPath);
        using SKBitmap rendered = Conversion.ToImage(
            pdfStream,
            page: 0,
            leaveOpen: false,
            password: null,
            options: new RenderOptions(Dpi: 144));

        // Resize to TargetWidth x TargetHeight with letterboxing.
        using var surface = SKSurface.Create(
            new SKImageInfo(TargetWidth, TargetHeight, SKColorType.Rgb888x, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        float scaleX = (float)TargetWidth / rendered.Width;
        float scaleY = (float)TargetHeight / rendered.Height;
        float scale = Math.Min(scaleX, scaleY);
        float drawW = rendered.Width * scale;
        float drawH = rendered.Height * scale;
        float offsetX = (TargetWidth - drawW) / 2f;
        float offsetY = (TargetHeight - drawH) / 2f;

        var destRect = new SKRect(offsetX, offsetY, offsetX + drawW, offsetY + drawH);
        canvas.DrawBitmap(rendered, destRect);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(outStream);
    }
#pragma warning restore CA1416
}
