namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Persists and restores per-book reading progress (FR-READ-001, Phase 04 schema).
/// Wraps <c>IReadingProgressRepository</c> and adds debounced writes so that the
/// repository is not hammered on every page turn.
/// </summary>
public interface IReadingProgressService
{
    /// <summary>
    /// Loads the saved reading progress for a book, or returns a default "unread"
    /// record if no progress exists.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The saved progress, or a new default record.</returns>
    Task<ReaderProgress> LoadAsync(string bookId, CancellationToken ct);

    /// <summary>
    /// Saves reading progress immediately (bypassing the debounce). Used when the
    /// session is closing to guarantee durability (NFR-OGMA-008).
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="progress">The progress to persist.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    Task SaveImmediateAsync(string bookId, ReaderProgress progress, CancellationToken ct);

    /// <summary>
    /// Schedules a debounced save that fires 500 ms after the last call. Multiple
    /// rapid calls (e.g. page turns) collapse into a single write.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="progress">The progress to persist.</param>
    void ScheduleSave(string bookId, ReaderProgress progress);
}

/// <summary>
/// Reading progress state persisted per book (FR-READ-001, FR-READ-003, FR-READ-004).
/// Extends the Phase 04 <c>ReadingProgress</c> domain entity with zoom and display mode.
/// </summary>
/// <param name="LastPageIndex">Zero-based index of the last page read.</param>
/// <param name="ScrollOffset">Vertical scroll offset in [0.0, 1.0] within the last page.</param>
/// <param name="ZoomMode">The zoom mode active when the book was last closed.</param>
/// <param name="ZoomPercent">The fixed zoom percentage (used when mode is Fixed).</param>
/// <param name="DisplayMode">The display mode active when the book was last closed.</param>
public sealed record ReaderProgress(
    int LastPageIndex,
    double ScrollOffset,
    ZoomMode ZoomMode,
    double ZoomPercent,
    DisplayMode DisplayMode)
{
    /// <summary>The default progress for a book that has never been opened.</summary>
    public static ReaderProgress Default { get; } = new(0, 0.0, ZoomMode.FitWidth, 100.0, DisplayMode.SinglePage);
}
