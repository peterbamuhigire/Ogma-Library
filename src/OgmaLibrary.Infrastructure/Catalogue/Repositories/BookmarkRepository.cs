using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookmarkRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal BookmarkRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BookmarkRepository"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public BookmarkRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Bookmark>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<BookmarkRow> rows = await context.Bookmarks
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
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookmarkRow? row = await context.Bookmarks
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task<Bookmark> CreateAsync(Bookmark bookmark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var row = new BookmarkRow
        {
            BookId = bookmark.BookId,
            Page = bookmark.PageIndex,
            Label = bookmark.Label,
            CreatedUtc = bookmark.CreatedUtc,
        };

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                context.Bookmarks.Add(row);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                context.Entry(row).State = EntityState.Detached;
                throw;
            }
        }

        return MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newLabel);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookmarkRow? row = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues originalValues =
            context.Entry(row).OriginalValues.Clone();

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                row.Label = newLabel;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                context.Entry(row).CurrentValues.SetValues(originalValues);
                context.Entry(row).State = EntityState.Unchanged;
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookmarkRow? row = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                context.Bookmarks.Remove(row);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                context.Entry(row).State = EntityState.Unchanged;
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

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
