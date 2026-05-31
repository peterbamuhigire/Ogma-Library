namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a named annotation layer
/// (HLD §3 — AnnotationLayers table, Phase 09 world-class addition).
/// </summary>
public sealed class AnnotationLayerRow
{
    /// <summary>The stable layer identifier (ULID-style 26-char string).</summary>
    public string LayerId { get; set; } = string.Empty;

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Display name of the layer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Layer highlight colour as a hex string (e.g. "#FFD700").</summary>
    public string Color { get; set; } = "#FFD700";

    /// <summary>Whether this layer's annotations are visible in the overlay.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Display sort order (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }

    /// <summary>Navigation: annotations that belong to this layer.</summary>
    public List<AnnotationV2Row> Annotations { get; set; } = [];
}
