namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a canonical work (HLD §3 — Works table).
/// A Work is the intellectual concept; an Edition is a specific published form.
/// The Work/Edition layer is schema-only in this phase (WP9); no merge/split UI is built.
/// </summary>
public sealed class WorkRow
{
    /// <summary>The stable work identifier.</summary>
    public long WorkId { get; set; }

    /// <summary>The canonical title of the work.</summary>
    public string? CanonicalTitle { get; set; }

    /// <summary>Optional FK to the primary author.</summary>
    public long? CanonicalAuthorId { get; set; }

    /// <summary>Navigation: editions of this work.</summary>
    public List<EditionRow> Editions { get; set; } = [];
}
