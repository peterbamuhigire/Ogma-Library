using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBookRepository"/> against
/// <see cref="CatalogueDbContext"/> (HLD §2.2, ADR-0005).
/// </summary>
public sealed class BookRepository : IBookRepository
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public BookRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Book?> FindAsync(BookId id, CancellationToken cancellationToken)
    {
        BookRow? row = await _context.Books
            .AsNoTracking()
            .Include(b => b.BookFiles)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.ShelfBooks).ThenInclude(sb => sb.Shelf)
            .FirstOrDefaultAsync(b => b.BookId == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Book book, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(book);

        BookRow? existing = await _context.Books
            .FirstOrDefaultAsync(b => b.BookId == book.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var row = MapToRow(book);
            _context.Books.Add(row);
        }
        else
        {
            existing.Title = book.Title;
            existing.Year = book.Year;
            existing.Rating = book.Rating;
            existing.IsbnNormalized = book.Isbn?.Normalized;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Book MapToDomain(BookRow row)
    {
        Isbn? isbn = null;
        if (row.IsbnNormalized is not null && Isbn.TryParse(row.IsbnNormalized, out Isbn parsed))
        {
            isbn = parsed;
        }

        var files = row.BookFiles.Select(f => new BookFile
        {
            RelativePath = f.RelativePath,
            ContentHash = new ContentHash(string.IsNullOrEmpty(f.RelativePath) ? new string('a', 64) : GeneratePlaceholderHash(f.RelativePath)),
            SizeBytes = 0,
            ModifiedUtc = f.LastSeenUtc,
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

        return new Book
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

    private static BookRow MapToRow(Book book) => new BookRow
    {
        BookId = book.Id.Value,
        Title = book.Title,
        IsbnNormalized = book.Isbn?.Normalized,
        Year = book.Year,
        Rating = book.Rating,
        Status = 0,
    };

    private static string GeneratePlaceholderHash(string seed)
    {
        // Stable 64-char hex placeholder for display — not a real SHA-256.
        int hash = seed.GetHashCode();
        return Math.Abs(hash).ToString("x8", System.Globalization.CultureInfo.InvariantCulture).PadRight(16, '0').PadRight(64, '0');
    }
}
