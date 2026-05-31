using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBookmarkRepository"/> against
/// <see cref="CatalogueDbContext"/> (FR-READ-007, NFR-OGMA-008).
/// All writes are committed inside an explicit transaction before returning,
/// ensuring durability across abnormal termination.
/// </summary>
public sealed class BookmarkRepository : IBookmarkRepository
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookmarkRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public BookmarkRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Bookmark>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        List<BookmarkRow> rows = await _context.Bookmarks
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(b => b.Page)
            .ThenBy(b => b.CreatedUtc)
            .Select(MapToDomain)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Bookmark?> FindAsync(long bookmarkId, CancellationToken cancellationToken)
    {
        BookmarkRow? row = await _context.Bookmarks
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task<Bookmark> CreateAsync(Bookmark bookmark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        var row = new BookmarkRow
        {
            BookId = bookmark.BookId,
            Page = bookmark.PageIndex,
            Label = bookmark.Label,
            CreatedUtc = bookmark.CreatedUtc,
        };

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                _context.Bookmarks.Add(row);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _context.Entry(row).State = EntityState.Detached;
                throw;
            }
        }

        return MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newLabel);

        BookmarkRow? row = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues originalValues =
            _context.Entry(row).OriginalValues.Clone();

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                row.Label = newLabel;
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _context.Entry(row).CurrentValues.SetValues(originalValues);
                _context.Entry(row).State = EntityState.Unchanged;
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken)
    {
        BookmarkRow? row = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                _context.Bookmarks.Remove(row);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _context.Entry(row).State = EntityState.Unchanged;
                throw;
            }
        }
    }

    private static Bookmark MapToDomain(BookmarkRow row) =>
        new()
        {
            Id = row.BookmarkId,
            BookId = row.BookId,
            PageIndex = row.Page,
            Label = row.Label,
            CreatedUtc = row.CreatedUtc,
        };
}
