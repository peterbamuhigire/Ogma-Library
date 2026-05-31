using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IShelfRepository"/> against
/// <see cref="CatalogueDbContext"/> (FR-CAT-003).
/// </summary>
public sealed class ShelfRepository : IShelfRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ShelfRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal ShelfRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ShelfRepository"/>.
    /// </summary>
    public ShelfRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shelf>> ListAsync(CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<ShelfRow> rows = await context.Shelves
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new Shelf
        {
            Id = r.ShelfId,
            Name = r.Name,
            IsSmart = r.ShelfType == 1,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task SaveAsync(Shelf shelf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ShelfRow? existing = await context.Shelves
            .FirstOrDefaultAsync(s => s.ShelfId == shelf.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Shelves.Add(new ShelfRow
            {
                ShelfId = shelf.Id,
                Name = shelf.Name,
                ShelfType = shelf.IsSmart ? 1 : 0,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.Name = shelf.Name;
            existing.ShelfType = shelf.IsSmart ? 1 : 0;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
