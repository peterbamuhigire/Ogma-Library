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
    /// Full-resolution page render width in pixels at the default (fit-width) zoom.
    /// This is the logical page-surface width (720) supersampled ×2 for crispness on
    /// HiDPI displays. Kept in one place so the cache key (bookId, page, widthPx) is
    /// identical between prefetch and the on-screen render.
    /// </summary>
    public const int PageWidthPx = 1440;
}
