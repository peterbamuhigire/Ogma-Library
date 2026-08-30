using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Redacted after-state history for personal reading curation.</summary>
public sealed class ReadingStateHistoryRow
{
    /// <summary>Database identifier.</summary>
    public long ReadingStateHistoryId { get; set; }

    /// <summary>Owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Reading status after the change.</summary>
    public ReadingStatus? ReadingStatus { get; set; }

    /// <summary>Rating after the change.</summary>
    public int? Rating { get; set; }

    /// <summary>Favourite flag after the change.</summary>
    public bool IsFavourite { get; set; }

    /// <summary>Short non-content reason for the change.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>UTC change timestamp.</summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>Owning book navigation.</summary>
    public BookRow? Book { get; set; }
}
