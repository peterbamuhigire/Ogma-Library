namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Canonical reader render parameters shared by the reader surface and the
/// <see cref="IPageRenderCache"/> prefetcher. Both must request the same width so
/// prefetched neighbour pages produce cache hits on the next/previous page turn
/// (NFR-OGMA-005, ≤ 100 ms P95).
/// </summary>
public static class ReaderRenderDefaults
{
    /// <summary>
    /// Initial render width used before the reader surface reports its displayed
    /// size and monitor scaling. Once the view is measured the width follows
    /// <see cref="ComputePageWidthPx"/> (Sept-23 Kaizen K33).
    /// </summary>
    public const int PageWidthPx = 1440;

    /// <summary>Width buckets keep cache keys stable while a window is resized.</summary>
    public const int WidthBucketPx = 128;

    /// <summary>Hard upper bound on the raster width.</summary>
    public const int MaxPageWidthPx = 4096;

    /// <summary>Smallest raster width worth requesting for a readable page.</summary>
    public const int MinPageWidthPx = 256;

    /// <summary>
    /// Upper bound on raster pixels (width × height) for one page, about 64 MB of
    /// decoded RGBA. Tall pages are narrowed so a single page cannot exhaust memory.
    /// </summary>
    public const long MaxPagePixels = 4096L * 4096L;

    /// <summary>
    /// Computes the device-pixel raster width for a page so text is drawn 1:1 with the
    /// screen: displayed width (DIP, already including zoom) × monitor render scaling,
    /// rounded up to a <see cref="WidthBucketPx"/> bucket and bounded by
    /// <see cref="MaxPageWidthPx"/> and <see cref="MaxPagePixels"/>.
    /// </summary>
    /// <param name="displayWidthDip">The on-screen page width in device-independent pixels, including zoom.</param>
    /// <param name="renderScaling">The monitor render scaling (1.0 = 96 dpi, 1.5 = 150 %).</param>
    /// <param name="heightToWidthRatio">Page height divided by page width; used for the pixel budget.</param>
    /// <returns>The bucketed, bounded raster width in pixels.</returns>
    public static int ComputePageWidthPx(double displayWidthDip, double renderScaling, double heightToWidthRatio = 1.414)
    {
        if (double.IsNaN(displayWidthDip) || double.IsInfinity(displayWidthDip) || displayWidthDip <= 0)
        {
            return PageWidthPx;
        }

        double scaling = double.IsNaN(renderScaling) || double.IsInfinity(renderScaling) || renderScaling <= 0
            ? 1.0
            : Math.Clamp(renderScaling, 0.5, 8.0);
        double ratio = double.IsNaN(heightToWidthRatio) || double.IsInfinity(heightToWidthRatio) || heightToWidthRatio <= 0
            ? 1.414
            : Math.Clamp(heightToWidthRatio, 0.05, 20.0);

        double devicePixels = Math.Ceiling(displayWidthDip * scaling);
        int bucketed = (int)Math.Min(
            MaxPageWidthPx,
            Math.Ceiling(devicePixels / WidthBucketPx) * WidthBucketPx);

        int pixelBudgetWidth = (int)Math.Floor(Math.Sqrt(MaxPagePixels / ratio));
        pixelBudgetWidth -= pixelBudgetWidth % WidthBucketPx;
        return Math.Clamp(Math.Min(bucketed, pixelBudgetWidth), MinPageWidthPx, MaxPageWidthPx);
    }
}
