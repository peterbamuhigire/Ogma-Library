namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Join row linking a shared classroom shelf to a catalogue book.</summary>
public sealed class SharedShelfBookRow
{
    /// <summary>Shared shelf identifier.</summary>
    public string ShelfId { get; set; } = string.Empty;

    /// <summary>Catalogue book identifier.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the book was added.</summary>
    public DateTimeOffset AddedUtc { get; set; }

    /// <summary>Navigation to the shared shelf.</summary>
    public SharedShelfRow? Shelf { get; set; }

    /// <summary>Navigation to the catalogue book.</summary>
    public BookRow? Book { get; set; }
}
