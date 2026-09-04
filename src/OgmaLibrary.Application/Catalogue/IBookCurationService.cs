using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>Mutable personal curation state for one catalogue book.</summary>
public interface IBookCurationService
{
    /// <summary>
    /// Updates one or more personal reading-state fields and records a redacted
    /// history snapshot. Null fields are left unchanged.
    /// </summary>
    Task UpdateReadingStateAsync(
        string bookId,
        ReadingStatus? readingStatus = null,
        int? rating = null,
        bool? isFavourite = null,
        string reason = "user",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a bounded, newest-first history of personal reading-state changes.
    /// Entries contain only state values and the short non-content reason saved by
    /// the curation boundary.
    /// </summary>
    Task<IReadOnlyList<ReadingStateHistoryEntry>> GetHistoryAsync(
        string bookId,
        int maxResults = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>Redacted personal reading-state history entry for local presentation.</summary>
public sealed record ReadingStateHistoryEntry(
    ReadingStatus? ReadingStatus,
    int? Rating,
    bool IsFavourite,
    string Reason,
    DateTimeOffset ChangedUtc);
