namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a metadata lookup result from an external provider
/// (HLD §3 — MetadataLookups table, FR-META-002).
/// </summary>
public sealed class MetadataLookupRow
{
    /// <summary>The stable lookup identifier.</summary>
    public long LookupId { get; set; }

    /// <summary>FK to the book this lookup was performed for.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The provider name (e.g. "GoogleBooks", "OpenLibrary").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The ISBN used to request metadata.</summary>
    public string? RequestIsbn { get; set; }

    /// <summary>The raw JSON response from the provider.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>UTC timestamp when this lookup was performed.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Confidence score for the match.</summary>
    public double? Confidence { get; set; }

    /// <summary>Whether the user applied this lookup's data to the catalogue.</summary>
    public bool Applied { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }
}
