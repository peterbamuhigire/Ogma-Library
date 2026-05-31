namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a physical book file (HLD §3 — BookFiles table).
/// A book may have multiple files (e.g., multiple formats or duplicates).
/// </summary>
public sealed class BookFileRow
{
    /// <summary>The stable file identifier.</summary>
    public long BookFileId { get; set; }

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The path relative to the library root (forward-slash separated).</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>File presence status (0=Present, 1=Missing, 2=Excluded).</summary>
    public int FileStatus { get; set; }

    /// <summary>UTC timestamp when this file was last seen on disk.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }

    /// <summary>Navigation: owning book.</summary>
    public BookRow? Book { get; set; }
}
