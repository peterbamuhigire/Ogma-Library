namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Emits a stream of reader events consumable by the local UI and — in Phase 16 —
/// by the LAN host projection layer. All callers depend only on this interface;
/// the event bus implementation stays behind the adapter boundary.
/// </summary>
/// <remarks>
/// LAN-ready design: the observable event stream is defined here in Phase 08 so
/// Phase 16 can wire it to the LAN host without retrofitting the architecture.
/// In Phase 08 only the ReaderViewModel is subscribed.
/// </remarks>
public interface IReaderSessionReadModel
{
    /// <summary>
    /// An observable sequence of reader events raised as the user navigates,
    /// zooms, changes display mode, or scrolls.
    /// </summary>
    IObservable<ReaderEvent> Events { get; }
}

/// <summary>
/// Discriminated union of reader session events (LAN-CLASSROOM-ARCHITECTURE §3).
/// </summary>
public abstract record ReaderEvent
{
    private ReaderEvent() { }

    /// <summary>The user navigated to a different page.</summary>
    /// <param name="BookId">The book whose page changed.</param>
    /// <param name="PageIndex">The new zero-based page index.</param>
    /// <param name="TotalPages">Total page count in the document.</param>
    public sealed record PageChanged(string BookId, int PageIndex, int TotalPages) : ReaderEvent;

    /// <summary>The zoom level changed.</summary>
    /// <param name="BookId">The affected book.</param>
    /// <param name="Mode">The new zoom mode.</param>
    /// <param name="Percent">The effective zoom percentage (e.g. 100.0 = 100 %).</param>
    public sealed record ZoomChanged(string BookId, ZoomMode Mode, double Percent) : ReaderEvent;

    /// <summary>The display mode changed.</summary>
    /// <param name="BookId">The affected book.</param>
    /// <param name="Mode">The new display mode.</param>
    public sealed record DisplayModeChanged(string BookId, DisplayMode Mode) : ReaderEvent;

    /// <summary>The scroll position changed.</summary>
    /// <param name="BookId">The affected book.</param>
    /// <param name="ScrollOffset">Vertical scroll offset within the current page, in [0.0, 1.0].</param>
    public sealed record Scrolled(string BookId, double ScrollOffset) : ReaderEvent;

    /// <summary>The reader session was opened for a book.</summary>
    /// <param name="BookId">The book that was opened.</param>
    public sealed record SessionOpened(string BookId) : ReaderEvent;

    /// <summary>The reader session was closed.</summary>
    /// <param name="BookId">The book whose session was closed.</param>
    public sealed record SessionClosed(string BookId) : ReaderEvent;
}

/// <summary>
/// Zoom modes available in the PDF reader (FR-READ-003).
/// </summary>
public enum ZoomMode
{
    /// <summary>Scale the page so its width fills the available container width.</summary>
    FitWidth = 0,

    /// <summary>Scale the page so the full page fits within the container without scrolling.</summary>
    FitPage = 1,

    /// <summary>Use a fixed percentage zoom stored in <c>ReadingProgress.ZoomPercent</c>.</summary>
    Fixed = 2,
}

/// <summary>
/// Page display modes available in the PDF reader (FR-READ-004).
/// </summary>
public enum DisplayMode
{
    /// <summary>Show one page at a time, centered in the viewport.</summary>
    SinglePage = 0,

    /// <summary>Show two pages side-by-side (even/odd spread layout).</summary>
    TwoPage = 1,

    /// <summary>Continuously scroll through all pages in a single vertically stacked list.</summary>
    Continuous = 2,
}
