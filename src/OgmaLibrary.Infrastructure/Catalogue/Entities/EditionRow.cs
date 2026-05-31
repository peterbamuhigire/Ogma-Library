namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a specific published edition (HLD §3 — Editions table).
/// Books carry a nullable FK to their edition when the Work/Edition layer is populated.
/// Schema-only in Phase 04; no merge/split UI is built here (deferred to Phase 06/07).
/// </summary>
public sealed class EditionRow
{
    /// <summary>The stable edition identifier.</summary>
    public long EditionId { get; set; }

    /// <summary>FK to the parent work.</summary>
    public long WorkId { get; set; }

    /// <summary>Language code (e.g. "en", "fr").</summary>
    public string? Language { get; set; }

    /// <summary>Publication year of this edition.</summary>
    public int? PublicationYear { get; set; }

    /// <summary>Publisher name for this edition.</summary>
    public string? Publisher { get; set; }

    /// <summary>Navigation: the parent work.</summary>
    public WorkRow? Work { get; set; }

    /// <summary>Navigation: books associated with this edition.</summary>
    public List<BookRow> Books { get; set; } = [];
}
