namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Administrator-curated shelf shared with classroom profiles.</summary>
public sealed class SharedShelfRow
{
    /// <summary>Stable shared shelf identifier.</summary>
    public string ShelfId { get; set; } = string.Empty;

    /// <summary>Visible shelf name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional shelf description.</summary>
    public string? Description { get; set; }

    /// <summary>Visibility enum value from the SchoolAdmin application contract.</summary>
    public int Visibility { get; set; }

    /// <summary>JSON array of group identifiers when visibility is group-scoped.</summary>
    public string GroupIdsJson { get; set; } = "[]";

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Soft-delete marker for reversible curation changes.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Books assigned to this shared shelf.</summary>
    public ICollection<SharedShelfBookRow> Books { get; } = new List<SharedShelfBookRow>();
}
