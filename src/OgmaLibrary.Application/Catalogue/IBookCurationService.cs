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
}
