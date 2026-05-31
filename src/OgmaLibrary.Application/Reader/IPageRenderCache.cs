namespace OgmaLibrary.Application.Reader;

/// <summary>
/// A memory-budgeted LRU cache that pre-renders the current page and its neighbours
/// so that cached page turns complete within the ≤ 100 ms P95 budget (NFR-OGMA-005).
/// All render work is off-thread and cancellable; the UI thread is never blocked.
/// </summary>
public interface IPageRenderCache
{
    /// <summary>
    /// Gets the cached render for the specified page, or initiates a render and returns
    /// the result when complete. If a low-res preview is available it is returned
    /// immediately while the full-res render is in progress.
    /// </summary>
    /// <param name="bookId">The stable book identity (cache key component).</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="request">Render parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// The render result. May be a low-res preview if the full render is still in
    /// progress; call again after the <see cref="RenderCompleted"/> event to get
    /// the full-res result.
    /// </returns>
    Task<RenderResult> GetOrRenderAsync(string bookId, int pageIndex, RenderRequest request, CancellationToken ct);

    /// <summary>
    /// Triggers background pre-renders for the specified page indices (typically
    /// current ± 1). Already-cached pages are skipped.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pages">The page indices to pre-render.</param>
    /// <param name="request">Render parameters.</param>
    void Prefetch(string bookId, IEnumerable<int> pages, RenderRequest request);

    /// <summary>
    /// Cancels all pending renders for pages outside the specified set and removes
    /// their partial results from the in-flight tracking table.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="keepPages">Pages whose renders should continue.</param>
    void CancelOutside(string bookId, IEnumerable<int> keepPages);

    /// <summary>
    /// Removes all cached entries for a book (called when the document is closed).
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    void Invalidate(string bookId);

    /// <summary>The current total memory used by cached bitmaps, in bytes.</summary>
    long MemoryUsageBytes { get; }

    /// <summary>
    /// Raised on the UI thread when a background render completes and the cache entry
    /// is updated with a full-res result.
    /// </summary>
    event EventHandler<RenderCompletedEventArgs>? RenderCompleted;
}

/// <summary>Event arguments for <see cref="IPageRenderCache.RenderCompleted"/>.</summary>
/// <param name="BookId">The book whose page finished rendering.</param>
/// <param name="PageIndex">The zero-based page index.</param>
/// <param name="Result">The completed render result.</param>
public sealed class RenderCompletedEventArgs(string BookId, int PageIndex, RenderResult Result) : EventArgs
{
    /// <summary>The book whose page finished rendering.</summary>
    public string BookId { get; } = BookId;

    /// <summary>The zero-based page index that finished rendering.</summary>
    public int PageIndex { get; } = PageIndex;

    /// <summary>The completed render result.</summary>
    public RenderResult Result { get; } = Result;
}
