namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a highlight or note anchored to a page region
/// (HLD §3 — Annotations table, FR-READ-008, ADR-0008).
/// </summary>
public sealed class AnnotationRow
{
    /// <summary>The stable annotation identifier.</summary>
    public string AnnotationId { get; set; } = string.Empty;

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Zero-based page index.</summary>
    public int Page { get; set; }

    /// <summary>Annotation type (0=Highlight, 1=Note).</summary>
    public int Type { get; set; }

    /// <summary>UTC timestamp when this annotation was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC timestamp when this annotation was last modified.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>Named colour key for highlights (e.g. "yellow", "blue").</summary>
    public string? ColorKey { get; set; }

    /// <summary>Navigation: the annotation body (quoted text, note, geometry).</summary>
    public AnnotationBodyRow? Body { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }
}
