namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Manages the lifecycle of a reader session: opening a document, resuming from the
/// last saved position, navigating pages, persisting progress, and closing cleanly.
/// Implements FR-READ-001 and FR-READ-002.
/// </summary>
public interface IReaderSessionService
{
    /// <summary>The currently active session, or <see langword="null"/> when no book is open.</summary>
    ReaderSession? CurrentSession { get; }

    /// <summary>The renderer for the currently open session, or <see langword="null"/> when no book is open.</summary>
    IPdfRenderer? CurrentRenderer { get; }

    /// <summary>
    /// Opens a PDF document for the specified book, restoring the last saved page and
    /// scroll offset (FR-READ-001). If no progress record exists the document opens
    /// at page 0.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageHint">
    /// Optional page override. When <see langword="null"/> the last saved page is used.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The new reader session.</returns>
    Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct);

    /// <summary>
    /// Opens a password-protected PDF document for the specified book.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageHint">
    /// Optional page override. When <see langword="null"/> the last saved page is used.
    /// </param>
    /// <param name="password">Password characters supplied by the OS credential flow.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The new reader session.</returns>
    Task<ReaderSession> OpenProtectedAsync(
        string bookId,
        int? pageHint,
        char[] password,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(password);
        return OpenAsync(bookId, pageHint, ct);
    }

    /// <summary>
    /// Closes the current session, flushing any pending progress write synchronously
    /// to ensure durability even if the application exits abnormally (NFR-OGMA-008).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    Task CloseAsync(CancellationToken ct);

    /// <summary>
    /// Navigates to the specified page, updates the session, triggers cache pre-render,
    /// and schedules a debounced progress save (500 ms).
    /// </summary>
    /// <param name="pageIndex">Zero-based target page index.</param>
    /// <param name="scrollOffset">Scroll offset within the new page in [0.0, 1.0].</param>
    Task NavigateToAsync(int pageIndex, double scrollOffset = 0.0);

    /// <summary>
    /// Updates the scroll offset for the current page and schedules a debounced progress save.
    /// </summary>
    /// <param name="scrollOffset">Scroll offset in [0.0, 1.0].</param>
    void UpdateScrollOffset(double scrollOffset);
}

/// <summary>
/// An active reader session for a single book (one session per open document).
/// </summary>
/// <param name="BookId">The stable book identity.</param>
/// <param name="FilePath">The absolute path to the PDF file on disk.</param>
/// <param name="PageCount">The total number of pages in the document.</param>
/// <param name="CurrentPageIndex">The current zero-based page index.</param>
/// <param name="ScrollOffset">The vertical scroll offset in [0.0, 1.0].</param>
/// <param name="ZoomMode">The active zoom mode.</param>
/// <param name="ZoomPercent">The effective zoom percentage (used when mode is Fixed).</param>
/// <param name="DisplayMode">The active page display mode.</param>
/// <param name="PageRotationDegrees">The current page rotation in PDF-standard degrees (0, 90, 180, 270).</param>
public sealed record ReaderSession(
    string BookId,
    string FilePath,
    int PageCount,
    int CurrentPageIndex,
    double ScrollOffset,
    ZoomMode ZoomMode,
    double ZoomPercent,
    DisplayMode DisplayMode,
    int PageRotationDegrees = 0)
{
    /// <summary>
    /// Returns a copy with the page index updated.
    /// </summary>
    /// <param name="pageIndex">The new page index.</param>
    /// <param name="scrollOffset">The new scroll offset.</param>
    /// <param name="pageRotationDegrees">The new page rotation, or <see langword="null"/> to keep the current value.</param>
    /// <returns>Updated session record.</returns>
    public ReaderSession WithPage(
        int pageIndex,
        double scrollOffset = 0.0,
        int? pageRotationDegrees = null) =>
        this with
        {
            CurrentPageIndex = pageIndex,
            ScrollOffset = scrollOffset,
            PageRotationDegrees = pageRotationDegrees ?? PageRotationDegrees,
        };

    /// <summary>Returns a copy with the zoom mode and percent updated.</summary>
    /// <param name="mode">The new zoom mode.</param>
    /// <param name="percent">The new zoom percentage.</param>
    /// <returns>Updated session record.</returns>
    public ReaderSession WithZoom(ZoomMode mode, double percent) =>
        this with { ZoomMode = mode, ZoomPercent = percent };

    /// <summary>Returns a copy with the display mode updated.</summary>
    /// <param name="mode">The new display mode.</param>
    /// <returns>Updated session record.</returns>
    public ReaderSession WithDisplayMode(DisplayMode mode) =>
        this with { DisplayMode = mode };
}
