namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core join row linking a book to an author with role and display order
/// (HLD §3 — BookAuthors table).
/// </summary>
public sealed class BookAuthorRow
{
    /// <summary>FK to the book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>FK to the author.</summary>
    public long AuthorId { get; set; }

    /// <summary>The author's role (e.g. "Author", "Editor", "Translator").</summary>
    public string? Role { get; set; }

    /// <summary>Display order of this author for this book (0-based).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Navigation: the book.</summary>
    public BookRow? Book { get; set; }

    /// <summary>Navigation: the author.</summary>
    public AuthorRow? Author { get; set; }
}
