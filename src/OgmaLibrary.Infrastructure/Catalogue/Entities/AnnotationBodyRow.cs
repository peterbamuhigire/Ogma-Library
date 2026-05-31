namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for the body of an annotation: quoted text, note,
/// and bounding-rectangle geometry (HLD §3 — AnnotationBodies table, ADR-0008).
/// This is a 1-to-1 dependent of <see cref="AnnotationRow"/>.
/// </summary>
public sealed class AnnotationBodyRow
{
    /// <summary>FK and PK — shares the PK of the owning annotation.</summary>
    public string AnnotationId { get; set; } = string.Empty;

    /// <summary>The highlighted/selected quote text, when applicable.</summary>
    public string? QuoteText { get; set; }

    /// <summary>The user note text, when applicable.</summary>
    public string? NoteText { get; set; }

    /// <summary>
    /// Bounding-rectangle geometry as a JSON string, e.g.
    /// <c>{"x1":0.1,"y1":0.2,"x2":0.4,"y2":0.3}</c>.
    /// </summary>
    public string? RectJson { get; set; }

    /// <summary>Navigation: the owning annotation.</summary>
    public AnnotationRow? Annotation { get; set; }
}
