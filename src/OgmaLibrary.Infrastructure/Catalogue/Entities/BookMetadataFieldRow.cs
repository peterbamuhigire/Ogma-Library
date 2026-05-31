namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a single metadata field with provenance tracking
/// (HLD §3 — BookMetadataFields table, FR-META-004).
/// </summary>
public sealed class BookMetadataFieldRow
{
    /// <summary>The field identifier.</summary>
    public long FieldId { get; set; }

    /// <summary>FK to the owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The metadata field name (e.g. "Title", "Author", "Publisher").</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The field value.</summary>
    public string? Value { get; set; }

    /// <summary>The source provider (e.g. "GoogleBooks", "OpenLibrary", "User").</summary>
    public string? Source { get; set; }

    /// <summary>UTC timestamp when this value was obtained from the source.</summary>
    public DateTimeOffset? SourceTimestamp { get; set; }

    /// <summary>Confidence score in [0.0, 1.0].</summary>
    public double? Confidence { get; set; }

    /// <summary>Whether the user has manually overridden this field value.</summary>
    public bool IsOverridden { get; set; }

    /// <summary>Navigation: owning book.</summary>
    public BookRow? Book { get; set; }
}
