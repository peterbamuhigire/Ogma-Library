namespace OgmaLibrary.Domain;

/// <summary>
/// A normalized bounding region for an annotation, stored as coordinates in [0, 1]
/// relative to the un-rotated page size (FR-READ-008, NFR-OGMA-008).
/// </summary>
/// <remarks>
/// <para>
/// All coordinates are relative to the un-rotated page dimensions so that reload on
/// a rotated page is correct: the overlay applies the page rotation matrix at render time
/// rather than baking the rotated coordinates into the stored value.
/// See <c>AnnotationRenderHelper.ToScreenRect</c> for the rotation transform.
/// </para>
/// </remarks>
/// <param name="PageIndex">Zero-based page index within the document.</param>
/// <param name="NormLeft">Left edge in [0, 1] relative to un-rotated page width.</param>
/// <param name="NormTop">Top edge in [0, 1] relative to un-rotated page height.</param>
/// <param name="NormWidth">Width in [0, 1] relative to un-rotated page width.</param>
/// <param name="NormHeight">Height in [0, 1] relative to un-rotated page height.</param>
public sealed record AnnotationRegion(
    int PageIndex,
    double NormLeft,
    double NormTop,
    double NormWidth,
    double NormHeight);

/// <summary>
/// A durable bookmark anchored to a page within a document (FR-READ-007).
/// </summary>
public sealed class Bookmark
{
    /// <summary>The stable bookmark identifier.</summary>
    public required long Id { get; init; }

    /// <summary>The stable identifier of the owning book.</summary>
    public required string BookId { get; init; }

    /// <summary>Zero-based page index where the bookmark is anchored.</summary>
    public required int PageIndex { get; init; }

    /// <summary>The user-assigned label, or <see langword="null"/> for auto-labelled bookmarks.</summary>
    public string? Label { get; set; }

    /// <summary>UTC timestamp when the bookmark was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// A named annotation layer that groups highlights and notes (world-class addition).
/// </summary>
public sealed class AnnotationLayer
{
    /// <summary>The stable layer identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The stable identifier of the owning book.</summary>
    public required string BookId { get; init; }

    /// <summary>The display name of the layer.</summary>
    public required string Name { get; set; }

    /// <summary>The layer colour as a hex string (e.g. <c>"#FFD700"</c>).</summary>
    public required string Color { get; set; }

    /// <summary>Whether the layer's annotations are visible in the overlay.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Display sort order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// An extended annotation that includes layer assignment and normalized region list
/// (FR-READ-008, Phase 09 extension).
/// </summary>
public sealed class AnnotationV2
{
    /// <summary>The stable annotation identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The stable identifier of the owning book.</summary>
    public required string BookId { get; init; }

    /// <summary>
    /// Version of the coordinate representation. Missing legacy values are treated
    /// as <see cref="AnnotationCoordinateContract.CurrentVersion"/> by persistence.
    /// </summary>
    public string CoordinateVersion { get; set; } = AnnotationCoordinateContract.CurrentVersion;

    /// <summary>The stable identifier of the layer this annotation belongs to, or <see langword="null"/> for the default layer.</summary>
    public string? LayerId { get; set; }

    /// <summary>The kind of annotation.</summary>
    public required AnnotationKind Kind { get; init; }

    /// <summary>Normalized bounding regions (one per text line for multi-line highlights).</summary>
    public required IReadOnlyList<AnnotationRegion> Regions { get; init; }

    /// <summary>The highlight colour as a hex string, when <see cref="Kind"/> is <see cref="AnnotationKind.Highlight"/>.</summary>
    public string? HighlightColor { get; set; }

    /// <summary>The highlighted / selected text, if available.</summary>
    public string? QuoteText { get; set; }

    /// <summary>The note text, when <see cref="Kind"/> is <see cref="AnnotationKind.Note"/>.</summary>
    public string? NoteText { get; set; }

    /// <summary>UTC timestamp when the annotation was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }

    /// <summary>UTC timestamp when the annotation was last modified.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}

/// <summary>
/// A citation card capturing the bibliographic context of a selected passage (FR-READ-011).
/// </summary>
/// <param name="BookId">The stable identifier of the source book.</param>
/// <param name="Title">The book title.</param>
/// <param name="Author">The primary author display name.</param>
/// <param name="PageNumber">The human-readable 1-based page number.</param>
/// <param name="SelectedText">The quoted passage.</param>
public sealed record CitationCard(
    string BookId,
    string? Title,
    string? Author,
    int PageNumber,
    string SelectedText)
{
    /// <summary>
    /// Formats the citation as a plain-text string suitable for clipboard export.
    /// Format: <c>"&lt;text&gt;" — Author, Title, p.N</c>
    /// </summary>
    /// <returns>The formatted citation string.</returns>
    public string ToPlainText(
        string unknownAuthor = "Unknown author",
        string unknownTitle = "Unknown title",
        string pageFormat = "p.{0}")
    {
        string author = Author ?? unknownAuthor;
        string title = Title ?? unknownTitle;
        string page = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            pageFormat,
            PageNumber);
        return $"\"{SelectedText}\" — {author}, {title}, {page}";
    }
}

/// <summary>
/// A reading-memory journal entry for a book (world-class addition, Phase 09).
/// Records why the reader opened the book, key insights, open questions, and their
/// overall disposition. Feeds the AI advisor in Phase 13.
/// </summary>
public sealed class ReadingMemory
{
    /// <summary>The stable identifier of the book this journal entry belongs to.</summary>
    public required string BookId { get; init; }

    /// <summary>Why the reader opened this book (free text).</summary>
    public string? OpenedBecause { get; set; }

    /// <summary>The reader's key insight or takeaway (free text).</summary>
    public string? KeyInsight { get; set; }

    /// <summary>Open questions raised by the reading (free text).</summary>
    public string? OpenQuestions { get; set; }

    /// <summary>
    /// Disposition score from 1 to 5 (1 = did not finish / not useful,
    /// 5 = transformative). <see langword="null"/> when not yet rated.
    /// </summary>
    public int? Disposition { get; set; }

    /// <summary>UTC timestamp when this entry was first created.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when this entry was last modified.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
