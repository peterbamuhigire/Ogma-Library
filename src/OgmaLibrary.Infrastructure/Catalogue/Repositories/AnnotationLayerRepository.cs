using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAnnotationLayerRepository"/> against
/// <see cref="CatalogueDbContext"/> (Phase 09 world-class addition).
/// </summary>
public sealed class AnnotationLayerRepository : IAnnotationLayerRepository
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationLayerRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public AnnotationLayerRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnotationLayer>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        List<AnnotationLayerRow> rows = await _context.AnnotationLayers
            .AsNoTracking()
            .Where(l => l.BookId == bookId)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.LayerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<AnnotationLayer?> FindAsync(
        string layerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        AnnotationLayerRow? row = await _context.AnnotationLayers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LayerId == layerId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task<AnnotationLayer> CreateAsync(
        AnnotationLayer layer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var row = new AnnotationLayerRow
        {
            LayerId = layer.Id,
            BookId = layer.BookId,
            Name = layer.Name,
            Color = layer.Color,
            IsVisible = layer.IsVisible,
            SortOrder = layer.SortOrder,
        };

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            _context.AnnotationLayers.Add(row);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(AnnotationLayer layer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layer);

        AnnotationLayerRow? row = await _context.AnnotationLayers
            .FirstOrDefaultAsync(l => l.LayerId == layer.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        row.Name = layer.Name;
        row.Color = layer.Color;
        row.IsVisible = layer.IsVisible;
        row.SortOrder = layer.SortOrder;

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string layerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        AnnotationLayerRow? row = await _context.AnnotationLayers
            .FirstOrDefaultAsync(l => l.LayerId == layerId, cancellationToken)
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
            _context.AnnotationLayers.Remove(row);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task MergeIntoAsync(
        string sourceLayerId,
        string targetLayerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLayerId);

        // Move all annotations from source to target layer.
        List<AnnotationV2Row> annotations = await _context.AnnotationsV2
            .Where(a => a.LayerId == sourceLayerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (AnnotationV2Row ann in annotations)
        {
            ann.LayerId = targetLayerId;
        }

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static AnnotationLayer MapToDomain(AnnotationLayerRow row) =>
        new()
        {
            Id = row.LayerId,
            BookId = row.BookId,
            Name = row.Name,
            Color = row.Color,
            IsVisible = row.IsVisible,
            SortOrder = row.SortOrder,
        };
}
