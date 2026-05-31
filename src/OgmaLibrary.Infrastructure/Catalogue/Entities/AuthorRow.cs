namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a distinct author (HLD §3 — Authors table).
/// Authors are normalized for consistent browsing and matching.
/// </summary>
public sealed class AuthorRow
{
    /// <summary>The stable author identifier.</summary>
    public long AuthorId { get; set; }

    /// <summary>The normalized author name used for deduplication and sorting.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>The sort name (typically "Surname, Given").</summary>
    public string? SortName { get; set; }

    /// <summary>Navigation: join records linking this author to books.</summary>
    public List<BookAuthorRow> BookAuthors { get; set; } = [];
}
