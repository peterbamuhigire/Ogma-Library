using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// EF Core implementation of <see cref="ICatalogueReadModel"/>.
/// Uses server-side LINQ projections to avoid loading entity graphs and to keep
/// the projected records EF-Core-free (LAN-CLASSROOM-ARCHITECTURE.md §2-3).
/// </summary>
public sealed class CatalogueReadModel : ICatalogueReadModel
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public CatalogueReadModel(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
        CatalogueFilter filter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            query = query.Where(b => b.Title != null && b.Title.Contains(filter.TitleContains));
        }

        if (!string.IsNullOrWhiteSpace(filter.ShelfId))
        {
            query = query.Where(b => b.ShelfBooks.Any(sb => sb.ShelfId == filter.ShelfId));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(b => b.Status == filter.Status.Value);
        }

        var projected = query
            .OrderBy(b => b.Title ?? string.Empty)
            .Select(b => new
            {
                b.BookId,
                b.Title,
                b.Status,
                b.Rating,
                b.Year,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                ShelfIds = b.ShelfBooks
                    .Select(sb => sb.ShelfId)
                    .ToList(),
                Progress = b.ReadingProgress,
                HasPresentFile = b.BookFiles.Any(f => f.FileStatus == 0),
            });

        if (filter.MaxResults > 0)
        {
            projected = projected.Take(filter.MaxResults);
        }

        var results = await projected.ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var item in results)
        {
            yield return new BookSummaryProjection(
                BookId: item.BookId,
                Title: item.Title,
                Authors: item.Authors,
                CoverRelativePath: null, // Phase 05 populates covers
                Status: item.Status,
                Rating: item.Rating,
                ShelfIds: item.ShelfIds,
                ReadingProgressPct: item.Progress?.CompletionPct,
                IsAvailable: item.HasPresentFile,
                Year: item.Year);
        }
    }

    /// <inheritdoc />
    public async Task<BookDetailProjection?> GetBookDetailAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        var result = await _context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new
            {
                b.BookId,
                b.Title,
                b.Year,
                b.IsbnNormalized,
                b.Doi,
                b.Rating,
                b.Status,
                b.RelativePath,
                b.Sha256Hash,
                b.SizeBytes,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                Progress = b.ReadingProgress,
                AnnotationCount = b.Annotations.Count,
                MetadataFields = b.MetadataFields
                    .Select(f => new { f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return null;
        }

        ReadingProgressProjection? progress = result.Progress is not null
            ? new ReadingProgressProjection(
                BookId: result.BookId,
                CurrentPage: result.Progress.CurrentPage,
                CompletionPct: result.Progress.CompletionPct,
                LastReadUtc: result.Progress.LastReadUtc,
                Status: result.Progress.Status)
            : null;

        var fields = result.MetadataFields
            .Select(f => new MetadataFieldProjection(f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden))
            .ToList();

        return new BookDetailProjection(
            BookId: result.BookId,
            Title: result.Title,
            Authors: result.Authors,
            Year: result.Year,
            Isbn: result.IsbnNormalized,
            Doi: result.Doi,
            Rating: result.Rating,
            Status: result.Status,
            CoverRelativePath: null,
            RelativePath: result.RelativePath,
            Sha256Hash: result.Sha256Hash,
            SizeBytes: result.SizeBytes,
            ReadingProgress: progress,
            Annotations: result.AnnotationCount,
            MetadataFields: fields);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var shelves = await _context.Shelves
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                s.ShelfId,
                s.Name,
                s.ShelfType,
                BookCount = s.ShelfBooks.Count,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var s in shelves)
        {
            yield return new ShelfProjection(
                ShelfId: s.ShelfId,
                Name: s.Name,
                IsSmart: s.ShelfType == 1,
                BookCount: s.BookCount);
        }
    }

    /// <inheritdoc />
    public async Task<ReadingProgressProjection?> GetProgressAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        var result = await _context.ReadingProgress
            .AsNoTracking()
            .Where(r => r.BookId == bookId)
            .Select(r => new { r.BookId, r.CurrentPage, r.CompletionPct, r.LastReadUtc, r.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? null
            : new ReadingProgressProjection(
                BookId: result.BookId,
                CurrentPage: result.CurrentPage,
                CompletionPct: result.CompletionPct,
                LastReadUtc: result.LastReadUtc,
                Status: result.Status);
    }
}
