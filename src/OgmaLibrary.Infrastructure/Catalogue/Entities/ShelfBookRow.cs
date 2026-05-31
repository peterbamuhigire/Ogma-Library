namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core join row linking a shelf to a book with display metadata
/// (HLD §3 — ShelfBooks table, FR-CAT-003).
/// </summary>
public sealed class ShelfBookRow
{
    /// <summary>FK to the shelf.</summary>
    public string ShelfId { get; set; } = string.Empty;

    /// <summary>FK to the book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this book was added to the shelf.</summary>
    public DateTimeOffset AddedUtc { get; set; }

    /// <summary>Display order of this book within the shelf (0-based).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Navigation: the shelf.</summary>
    public ShelfRow? Shelf { get; set; }

    /// <summary>Navigation: the book.</summary>
    public BookRow? Book { get; set; }
}
