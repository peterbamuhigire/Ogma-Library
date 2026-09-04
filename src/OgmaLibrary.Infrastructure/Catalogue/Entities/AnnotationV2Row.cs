using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a Phase 09 extended annotation with layer
/// assignment, normalized region JSON, and quoted text
/// (HLD §3 — Annotations table extended, FR-READ-008, ADR-0008).
/// </summary>
public sealed class AnnotationV2Row
{
    /// <summary>The stable annotation identifier (ULID-style 26-char string).</summary>
    public string AnnotationId { get; set; } = string.Empty;

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Version of the serialized coordinate representation.</summary>
    public string CoordinateVersion { get; set; } = AnnotationCoordinateContract.CurrentVersion;

    /// <summary>FK to the annotation layer, or <see langword="null"/> for the implicit default layer.</summary>
    public string? LayerId { get; set; }

    /// <summary>Annotation type (0 = Highlight, 1 = Note).</summary>
    public int Type { get; set; }

    /// <summary>
    /// Normalized bounding regions serialized as JSON array:
    /// <c>[{"p":0,"l":0.1,"t":0.2,"w":0.3,"h":0.05}]</c>
    /// where p=pageIndex, l=normLeft, t=normTop, w=normWidth, h=normHeight.
    /// </summary>
    public string RegionsJson { get; set; } = "[]";

    /// <summary>Highlight colour as a hex string.</summary>
    public string? ColorKey { get; set; }

    /// <summary>The highlighted / selected text.</summary>
    public string? QuoteText { get; set; }

    /// <summary>The user note text, when Type = 1.</summary>
    public string? NoteText { get; set; }

    /// <summary>UTC timestamp when the annotation was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the annotation was last modified.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }

    /// <summary>Navigation: the annotation layer (nullable).</summary>
    public AnnotationLayerRow? Layer { get; set; }
}
