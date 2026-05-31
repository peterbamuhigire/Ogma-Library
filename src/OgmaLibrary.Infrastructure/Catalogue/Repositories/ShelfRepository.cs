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
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ShelfRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public ShelfRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shelf>> ListAsync(CancellationToken cancellationToken)
    {
        List<ShelfRow> rows = await _context.Shelves
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

        ShelfRow? existing = await _context.Shelves
            .FirstOrDefaultAsync(s => s.ShelfId == shelf.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _context.Shelves.Add(new ShelfRow
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

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
