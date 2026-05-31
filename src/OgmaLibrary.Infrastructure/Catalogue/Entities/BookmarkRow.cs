namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a durable bookmark (HLD §3 — Bookmarks table,
/// FR-READ-007).
/// </summary>
public sealed class BookmarkRow
{
    /// <summary>The stable bookmark identifier.</summary>
    public long BookmarkId { get; set; }

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Zero-based page index where the bookmark is anchored.</summary>
    public int Page { get; set; }

    /// <summary>Optional user-assigned label for the bookmark.</summary>
    public string? Label { get; set; }

    /// <summary>UTC timestamp when this bookmark was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }
}
