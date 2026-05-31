namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for per-book reading progress (HLD §3 — ReadingProgress table,
/// FR-READ-001). The primary key is BookId — one progress record per book.
/// </summary>
public sealed class ReadingProgressRow
{
    /// <summary>FK and PK — there is exactly one progress record per book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Zero-based index of the current page.</summary>
    public int CurrentPage { get; set; }

    /// <summary>Vertical scroll offset in pixels within the current page.</summary>
    public double ScrollOffsetPx { get; set; }

    /// <summary>UTC timestamp when this book was last read.</summary>
    public DateTimeOffset? LastReadUtc { get; set; }

    /// <summary>Cumulative count of pages read across all sessions.</summary>
    public int TotalPagesRead { get; set; }

    /// <summary>Reading completion percentage in [0.0, 100.0].</summary>
    public double CompletionPct { get; set; }

    /// <summary>Reading status (0=Unread, 1=Reading, 2=Finished, 3=Abandoned).</summary>
    public int Status { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }
}
