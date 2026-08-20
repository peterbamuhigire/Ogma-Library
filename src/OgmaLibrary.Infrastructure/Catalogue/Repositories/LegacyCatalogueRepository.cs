using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core compatibility adapter for legacy <c>Books</c> rows during the canonical
/// identity migration. It never fabricates unavailable file facts.
/// </summary>
public sealed class LegacyCatalogueRepository : ILegacyCatalogueRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="LegacyCatalogueRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal LegacyCatalogueRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LegacyCatalogueRepository"/>.
    /// </summary>
    public LegacyCatalogueRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<LegacyCatalogueRecord?> FindAsync(
        BookId id,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? row = await context.Books
            .AsNoTracking()
            .Include(b => b.BookFiles)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.ShelfBooks).ThenInclude(sb => sb.Shelf)
            .FirstOrDefaultAsync(b => b.BookId == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        LegacyCatalogueRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? existing = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == record.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var row = MapToRow(record);
            context.Books.Add(row);
        }
        else
        {
            existing.Title = record.Title;
            existing.Year = record.Year;
            existing.Rating = record.Rating;
            existing.IsbnNormalized = record.Isbn?.Normalized;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LegacyCatalogueRecord MapToDomain(BookRow row)
    {
        Isbn? isbn = null;
        if (row.IsbnNormalized is not null && Isbn.TryParse(row.IsbnNormalized, out Isbn parsed))
        {
            isbn = parsed;
        }

        ContentHash? contentHash = TryReadVerifiedHash(row.Sha256Hash);
        var files = row.BookFiles.Select(f => new LegacyFileRecord
        {
            RelativePath = f.RelativePath,
            ContentHash = contentHash,
            SizeBytes = row.SizeBytes,
            ModifiedUtc = row.MtimeTicks is long ticks
                ? new DateTimeOffset(ticks, TimeSpan.Zero)
                : null,
            Availability = f.FileStatus == 0 ? AvailabilityStatus.Available : AvailabilityStatus.Unavailable,
        }).ToList();

        var authors = row.BookAuthors
            .Where(ba => ba.Author is not null)
            .Select(ba => new Author
            {
                Name = ba.Author!.NormalizedName,
                NormalizedName = ba.Author.NormalizedName,
            }).ToList();

        var shelves = row.ShelfBooks
            .Where(sb => sb.Shelf is not null)
            .Select(sb => new Shelf
            {
                Id = sb.Shelf!.ShelfId,
                Name = sb.Shelf.Name,
                IsSmart = sb.Shelf.ShelfType == 1,
            }).ToList();

        return new LegacyCatalogueRecord
        {
            Id = new BookId(row.BookId),
            Title = row.Title,
            Isbn = isbn,
            Year = row.Year,
            Rating = row.Rating,
            Files = files,
            Authors = authors,
            Shelves = shelves,
        };
    }

    private static BookRow MapToRow(LegacyCatalogueRecord record) => new BookRow
    {
        BookId = record.Id.Value,
        Title = record.Title,
        IsbnNormalized = record.Isbn?.Normalized,
        Year = record.Year,
        Rating = record.Rating,
        Status = 0,
    };

    private static ContentHash? TryReadVerifiedHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new ContentHash(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
