namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a virtual or smart shelf (HLD §3 — Shelves table,
/// FR-CAT-003). Smart shelves have their membership defined by a saved query.
/// </summary>
public sealed class ShelfRow
{
    /// <summary>The stable shelf identifier.</summary>
    public string ShelfId { get; set; } = string.Empty;

    /// <summary>The shelf display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Shelf type (0=Virtual, 1=Smart).</summary>
    public int ShelfType { get; set; }

    /// <summary>The filter query JSON for smart shelves, null for virtual shelves.</summary>
    public string? Query { get; set; }

    /// <summary>Display order in the shelf list (0-based).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>UTC timestamp when this shelf was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Navigation: join records linking this shelf to books.</summary>
    public List<ShelfBookRow> ShelfBooks { get; set; } = [];
}
