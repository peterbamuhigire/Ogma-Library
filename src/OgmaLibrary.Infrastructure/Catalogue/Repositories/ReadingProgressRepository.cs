using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReadingProgressRepository"/> against
/// <see cref="CatalogueDbContext"/> (FR-READ-001).
/// </summary>
public sealed class ReadingProgressRepository : IReadingProgressRepository
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingProgressRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public ReadingProgressRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ReadingProgress?> GetAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        ReadingProgressRow? row = await _context.ReadingProgress
            .AsNoTracking()
            .Where(r => _context.BookFiles
                .Any(f => f.RelativePath == relativePath && f.BookId == r.BookId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string relativePath,
        ReadingProgress progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(progress);

        BookFileRow? fileRow = await _context.BookFiles
            .FirstOrDefaultAsync(f => f.RelativePath == relativePath, cancellationToken)
            .ConfigureAwait(false);

        if (fileRow is null)
        {
            throw new InvalidOperationException(
                $"No book file found for relative path '{relativePath}'. " +
                "Import the book before saving reading progress.");
        }

        ReadingProgressRow? existing = await _context.ReadingProgress
            .FirstOrDefaultAsync(r => r.BookId == fileRow.BookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _context.ReadingProgress.Add(new ReadingProgressRow
            {
                BookId = fileRow.BookId,
                CurrentPage = progress.LastPageIndex,
                ScrollOffsetPx = progress.LastScrollOffset,
                LastReadUtc = progress.LastOpenedUtc,
                Status = (int)progress.Status,
            });
        }
        else
        {
            existing.CurrentPage = progress.LastPageIndex;
            existing.ScrollOffsetPx = progress.LastScrollOffset;
            existing.LastReadUtc = progress.LastOpenedUtc;
            existing.Status = (int)progress.Status;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReadingProgress?> GetByBookIdAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        ReadingProgressRow? row = await _context.ReadingProgress
            .AsNoTracking()
            .Where(r => r.BookId == bookId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task SaveByBookIdAsync(
        string bookId,
        ReadingProgress progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(progress);

        ReadingProgressRow? existing = await _context.ReadingProgress
            .FirstOrDefaultAsync(r => r.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // Ensure the book exists before inserting progress.
            bool bookExists = await _context.Books
                .AnyAsync(b => b.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            if (!bookExists)
            {
                return; // Silently skip — book was removed.
            }

            _context.ReadingProgress.Add(new ReadingProgressRow
            {
                BookId = bookId,
                CurrentPage = progress.LastPageIndex,
                ScrollOffsetPx = progress.LastScrollOffset,
                LastReadUtc = progress.LastOpenedUtc,
                Status = (int)progress.Status,
            });
        }
        else
        {
            existing.CurrentPage = progress.LastPageIndex;
            existing.ScrollOffsetPx = progress.LastScrollOffset;
            existing.LastReadUtc = progress.LastOpenedUtc;
            existing.Status = (int)progress.Status;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ReadingProgress MapToDomain(ReadingProgressRow row) => new ReadingProgress
    {
        LastPageIndex = row.CurrentPage,
        LastScrollOffset = row.ScrollOffsetPx,
        LastOpenedUtc = row.LastReadUtc,
        Status = (ReadingStatus)row.Status,
    };
}
