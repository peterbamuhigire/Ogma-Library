namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a reading-memory journal entry
/// (Phase 09 world-class addition).
/// </summary>
public sealed class ReadingMemoryRow
{
    /// <summary>FK and PK — one row per book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Why the reader opened this book (free text, nullable).</summary>
    public string? OpenedBecause { get; set; }

    /// <summary>The reader's key insight (free text, nullable).</summary>
    public string? KeyInsight { get; set; }

    /// <summary>Open questions raised by the reading (free text, nullable).</summary>
    public string? OpenQuestions { get; set; }

    /// <summary>Disposition score 1–5 (nullable until rated).</summary>
    public int? Disposition { get; set; }

    /// <summary>UTC timestamp of first creation.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp of last update.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }
}
