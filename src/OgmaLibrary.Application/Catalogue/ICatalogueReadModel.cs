namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Read-only projection interface for the Library Catalogue bounded context.
/// Exposes LAN-safe, EF-Core-free projection records that Phases 06, 16-18 consume
/// (LAN-CLASSROOM-ARCHITECTURE.md §2-3).
/// </summary>
/// <remarks>
/// <para>
/// All returned types are plain C# records with no EF Core navigation properties.
/// They are safe to serialize over the LAN wire without leaking the entity graph.
/// </para>
/// <para>
/// Implementations may use EF Core LINQ projections internally but must never
/// expose <c>Microsoft.EntityFrameworkCore</c> types through this interface.
/// </para>
/// </remarks>
public interface ICatalogueReadModel
{
    /// <summary>
    /// Returns a filtered, paged stream of book summaries ordered by title.
    /// This is the primary feed for the catalogue grid, 3D shelf, and LAN clients.
    /// </summary>
    /// <param name="filter">Optional filter and pagination parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An async stream of <see cref="BookSummaryProjection"/> records; no EF
    /// navigation properties are included.
    /// </returns>
    IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
        CatalogueFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full detail for a single book across all five metadata groups.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The book detail projection, or <see langword="null"/> if no book with that
    /// identity exists.
    /// </returns>
    Task<BookDetailProjection?> GetBookDetailAsync(
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all virtual and smart shelves.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async stream of shelf projections ordered by display order.</returns>
    IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns the current reading progress for a book.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The progress projection, or <see langword="null"/> if the book was never opened.
    /// </returns>
    Task<ReadingProgressProjection?> GetProgressAsync(
        string bookId,
        CancellationToken cancellationToken = default);
}
