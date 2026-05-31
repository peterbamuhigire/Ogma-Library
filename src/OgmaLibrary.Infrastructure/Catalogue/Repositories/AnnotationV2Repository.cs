using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAnnotationV2Repository"/> against
/// <see cref="CatalogueDbContext"/> (FR-READ-008, NFR-OGMA-008, ADR-0008).
/// All create operations use an explicit transaction committed before returning.
/// </summary>
public sealed class AnnotationV2Repository : IAnnotationV2Repository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationV2Repository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal AnnotationV2Repository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationV2Repository"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public AnnotationV2Repository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnotationV2>> ListForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<AnnotationV2Row> rows = await context.AnnotationsV2
            .AsNoTracking()
            .Where(a => a.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(a => a.CreatedUtc)
            .Select(MapToDomain)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnotationV2>> ListForPageAsync(
        string bookId,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Regions JSON contains the pageIndex; we filter server-side by bookId
        // then client-side by page for simplicity (page count is small).
        List<AnnotationV2Row> rows = await context.AnnotationsV2
            .AsNoTracking()
            .Where(a => a.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(a => a.CreatedUtc)
            .Select(MapToDomain)
            .Where(a => a.Regions.Any(r => r.PageIndex == pageIndex))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AnnotationV2?> FindAsync(
        string annotationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        AnnotationV2Row? row = await context.AnnotationsV2
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AnnotationId == annotationId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task<AnnotationV2> CreateAsync(
        AnnotationV2 annotation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var row = new AnnotationV2Row
        {
            AnnotationId = annotation.Id,
            BookId = annotation.BookId,
            LayerId = annotation.LayerId,
            Type = (int)annotation.Kind,
            RegionsJson = SerializeRegions(annotation.Regions),
            ColorKey = annotation.HighlightColor,
            QuoteText = annotation.QuoteText,
            NoteText = annotation.NoteText,
            CreatedUtc = annotation.CreatedUtc,
            ModifiedUtc = annotation.ModifiedUtc,
        };

        var tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                context.AnnotationsV2.Add(row);
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
    public async Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        AnnotationV2Row? row = await context.AnnotationsV2
            .FirstOrDefaultAsync(a => a.AnnotationId == annotation.Id, cancellationToken)
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
                row.LayerId = annotation.LayerId;
                row.ColorKey = annotation.HighlightColor;
                row.NoteText = annotation.NoteText;
                row.QuoteText = annotation.QuoteText;
                row.RegionsJson = SerializeRegions(annotation.Regions);
                row.ModifiedUtc = annotation.ModifiedUtc;

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
    public async Task DeleteAsync(string annotationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        AnnotationV2Row? row = await context.AnnotationsV2
            .FirstOrDefaultAsync(a => a.AnnotationId == annotationId, cancellationToken)
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
                context.AnnotationsV2.Remove(row);
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

    private static AnnotationV2 MapToDomain(AnnotationV2Row row) =>
        new()
        {
            Id = row.AnnotationId,
            BookId = row.BookId,
            LayerId = row.LayerId,
            Kind = row.Type == 0 ? AnnotationKind.Highlight : AnnotationKind.Note,
            Regions = DeserializeRegions(row.RegionsJson),
            HighlightColor = row.ColorKey,
            QuoteText = row.QuoteText,
            NoteText = row.NoteText,
            CreatedUtc = row.CreatedUtc,
            ModifiedUtc = row.ModifiedUtc,
        };

    private static string SerializeRegions(IReadOnlyList<AnnotationRegion> regions)
    {
        var dtos = regions.Select(r => new
        {
            p = r.PageIndex,
            l = r.NormLeft,
            t = r.NormTop,
            w = r.NormWidth,
            h = r.NormHeight,
        });
        return JsonSerializer.Serialize(dtos);
    }

    private static List<AnnotationRegion> DeserializeRegions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            var list = new List<AnnotationRegion>();
            foreach (JsonElement el in doc.RootElement.EnumerateArray())
            {
                int p = el.TryGetProperty("p", out JsonElement pProp) ? pProp.GetInt32() : 0;
                double l = el.TryGetProperty("l", out JsonElement lProp) ? lProp.GetDouble() : 0;
                double t = el.TryGetProperty("t", out JsonElement tProp) ? tProp.GetDouble() : 0;
                double w = el.TryGetProperty("w", out JsonElement wProp) ? wProp.GetDouble() : 0;
                double h = el.TryGetProperty("h", out JsonElement hProp) ? hProp.GetDouble() : 0;
                list.Add(new AnnotationRegion(p, l, t, w, h));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

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
