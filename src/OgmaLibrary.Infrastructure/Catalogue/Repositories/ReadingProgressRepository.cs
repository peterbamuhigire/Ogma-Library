using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReadingProgressRepository"/> against
/// <see cref="CatalogueDbContext"/> (FR-READ-001).
/// </summary>
public sealed class ReadingProgressRepository : IReadingProgressRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingProgressRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal ReadingProgressRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingProgressRepository"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public ReadingProgressRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<ReadingProgress?> GetAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ReadingProgressRow? row = await context.ReadingProgress
            .AsNoTracking()
            .Where(r => context.BookFiles
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

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookFileRow? fileRow = await context.BookFiles
            .FirstOrDefaultAsync(f => f.RelativePath == relativePath, cancellationToken)
            .ConfigureAwait(false);

        if (fileRow is null)
        {
            throw new InvalidOperationException(
                $"No book file found for relative path '{relativePath}'. " +
                "Import the book before saving reading progress.");
        }

        ReadingProgressRow? existing = await context.ReadingProgress
            .FirstOrDefaultAsync(r => r.BookId == fileRow.BookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.ReadingProgress.Add(new ReadingProgressRow
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReadingProgress?> GetByBookIdAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ReadingProgressRow? row = await context.ReadingProgress
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

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ReadingProgressRow? existing = await context.ReadingProgress
            .FirstOrDefaultAsync(r => r.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // Ensure the book exists before inserting progress.
            bool bookExists = await context.Books
                .AnyAsync(b => b.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            if (!bookExists)
            {
                return; // Silently skip — book was removed.
            }

            context.ReadingProgress.Add(new ReadingProgressRow
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ReadingProgress MapToDomain(ReadingProgressRow row) => new ReadingProgress
    {
        LastPageIndex = row.CurrentPage,
        LastScrollOffset = row.ScrollOffsetPx,
        LastOpenedUtc = row.LastReadUtc,
        Status = (ReadingStatus)row.Status,
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
