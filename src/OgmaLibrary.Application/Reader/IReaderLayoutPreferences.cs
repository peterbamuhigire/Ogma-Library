namespace OgmaLibrary.Application.Reader;

/// <summary>Limits for the reader side panel width.</summary>
public static class ReaderSidePanelWidths
{
    /// <summary>The narrowest width; four tab headers fit on one row at this width.</summary>
    public const double Minimum = 320;

    /// <summary>The widest width.</summary>
    public const double Maximum = 640;

    /// <summary>The width used until the user resizes the panel.</summary>
    public const double Default = 320;

    /// <summary>Clamps a width into the supported range; a non-finite value gives the default.</summary>
    /// <param name="width">The requested width.</param>
    /// <returns>The clamped width.</returns>
    public static double Clamp(double width) =>
        double.IsFinite(width) ? Math.Clamp(width, Minimum, Maximum) : Default;
}

/// <summary>A reader page layout and its spread option (FR-READ-004).</summary>
/// <param name="Mode">The page display mode.</param>
/// <param name="CoverAlone">Whether a spread shows the cover alone before pairing the rest.</param>
public sealed record ReaderLayoutChoice(DisplayMode Mode, bool CoverAlone)
{
    /// <summary>One page at a time, the factory default.</summary>
    public static ReaderLayoutChoice Default { get; } = new(DisplayMode.SinglePage, true);
}

/// <summary>
/// Stores the reader layout per book and the global default used for books that have none
/// (FR-READ-004). Layout is a view preference, so it never touches the catalogue schema.
/// </summary>
public interface IReaderLayoutPreferences
{
    /// <summary>The layout used for a book that has no saved layout.</summary>
    ReaderLayoutChoice GlobalDefault { get; }

    /// <summary>Changes the global default layout.</summary>
    /// <param name="choice">The new default.</param>
    void SetDefault(ReaderLayoutChoice choice);

    /// <summary>Returns the saved layout of a book, or the default when none is saved.</summary>
    /// <param name="bookId">The book identifier.</param>
    ReaderLayoutChoice GetForBook(string bookId);

    /// <summary>Saves the layout of one book.</summary>
    /// <param name="bookId">The book identifier.</param>
    /// <param name="choice">The layout to remember.</param>
    void SetForBook(string bookId, ReaderLayoutChoice choice);

    /// <summary>The saved width of the reader side panel, in device-independent pixels.</summary>
    double SidePanelWidth { get; }

    /// <summary>Saves the side panel width.</summary>
    /// <param name="width">The width; the store clamps it to the supported range.</param>
    void SetSidePanelWidth(double width);
}
