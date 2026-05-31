using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using PDFtoImage;
using SkiaSharp;
#pragma warning disable CA1416 // Platform compatibility — PDFtoImage targets Windows/macOS/Linux (Ogma MVP platforms)

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Renders page 0 of a PDF at low resolution, scales to a 7×100 spine strip,
/// and writes a JPEG to the sidecar (FR-LIB-005, used by 3D shelf in Phase 14).
/// Always runs off the UI thread via <c>Task.Run</c>.
/// </summary>
public sealed class SpineService : ISpineService
{
    private const int SpineWidth = 7;
    private const int SpineHeight = 100;

    private readonly ISidecarService _sidecar;

    /// <summary>
    /// Initializes a new instance of <see cref="SpineService"/>.
    /// </summary>
    /// <param name="sidecar">The sidecar service used to resolve output paths.</param>
    public SpineService(ISidecarService sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        _sidecar = sidecar;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> GenerateSpineAsync(
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
            string outputPath = _sidecar.Resolve(contentHash, SidecarClass.Spines);

            await Task.Run(() =>
            {
                RenderAndSaveSpine(absoluteFilePath, outputPath);
            }, cancellationToken).ConfigureAwait(false);

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static void RenderAndSaveSpine(string pdfPath, string outputPath)
    {
        // Render page 0 at 36 DPI (low-res is sufficient for a 7px wide strip).
        using var pdfStream = File.OpenRead(pdfPath);
        using SKBitmap rendered = Conversion.ToImage(
            pdfStream,
            page: 0,
            leaveOpen: false,
            password: null,
            options: new RenderOptions(Dpi: 36));

        // Scale to SpineWidth x SpineHeight.
        using var surface = SKSurface.Create(
            new SKImageInfo(SpineWidth, SpineHeight, SKColorType.Rgb888x, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var destRect = new SKRect(0, 0, SpineWidth, SpineHeight);
        canvas.DrawBitmap(rendered, destRect);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(outStream);
    }
}
#pragma warning restore CA1416
