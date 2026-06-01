using OgmaLibrary.Application.Extensions;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ai.Extensions;

/// <summary>
/// Read-only local catalogue view for AI use cases. Phase 23's Extension SDK
/// will review this surface before publishing it to plugins.
/// </summary>
[ExtensionPoint]
internal interface IAiCatalogueReader
{
    /// <summary>Returns metadata for one local book, or null when the book is absent.</summary>
    Task<BookMetadataDto?> GetByIdAsync(BookId bookId, CancellationToken cancellationToken);

    /// <summary>Returns metadata for local books assigned to the supplied shelf.</summary>
    Task<IReadOnlyList<BookMetadataDto>> GetByShelfAsync(string shelfId, CancellationToken cancellationToken);
}
