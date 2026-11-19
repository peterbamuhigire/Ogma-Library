using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// The one definition of a duplicate candidate: books that share a normalised ISBN, or share a
/// content hash. Used by the library health dashboard and by the catalogue "Possible duplicates"
/// criterion (Sept-23 Phase 10, task 10.9), so both list the same books.
/// </summary>
internal static class DuplicateCandidates
{
    /// <summary>Reason code of an ISBN match.</summary>
    public const string IsbnReason = "ISBN";

    /// <summary>Reason code of a content-hash match.</summary>
    public const string ContentHashReason = "ContentHash";

    /// <summary>Loads every duplicate candidate; a book may appear once per reason.</summary>
    /// <param name="context">The catalogue context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The candidates, ISBN matches first.</returns>
    public static async Task<IReadOnlyList<DuplicateBookEntry>> LoadAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Load all books with an ISBN or hash; group client-side for SQLite compatibility.
        var booksWithIsbn = await context.Books
            .AsNoTracking()
            .Where(b => b.IsbnNormalized != null)
            .Select(b => new { b.BookId, b.Title, b.IsbnNormalized })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<DuplicateBookEntry>();
        foreach (var group in booksWithIsbn.GroupBy(b => b.IsbnNormalized!).Where(g => g.Count() > 1))
        {
            foreach (var book in group)
            {
                result.Add(new DuplicateBookEntry(book.BookId, book.Title, group.Key, IsbnReason));
            }
        }

        var booksWithHash = await context.Books
            .AsNoTracking()
            .Where(b => b.Sha256Hash != null)
            .Select(b => new { b.BookId, b.Title, b.Sha256Hash })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var group in booksWithHash.GroupBy(b => b.Sha256Hash!).Where(g => g.Count() > 1))
        {
            foreach (var book in group)
            {
                result.Add(new DuplicateBookEntry(book.BookId, book.Title, group.Key, ContentHashReason));
            }
        }

        return result;
    }

    /// <summary>The ids of every duplicate candidate.</summary>
    /// <param name="context">The catalogue context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The book ids.</returns>
    public static async Task<HashSet<string>> LoadBookIdsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken) =>
        (await LoadAsync(context, cancellationToken).ConfigureAwait(false))
            .Select(entry => entry.BookId)
            .ToHashSet(StringComparer.Ordinal);
}
