namespace OgmaLibrary.Application.Reader;

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
}
