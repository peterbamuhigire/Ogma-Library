using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>Persists validated personal reading-state changes and history.</summary>
public sealed class BookCurationService : IBookCurationService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Creates a service backed by an explicit context for tests.</summary>
    public BookCurationService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Creates a service backed by independent contexts for production.</summary>
    public BookCurationService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task UpdateReadingStateAsync(
        string bookId,
        ReadingStatus? readingStatus = null,
        int? rating = null,
        bool? isFavourite = null,
        string reason = "user",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        if (readingStatus is null && rating is null && isFavourite is null)
        {
            throw new ArgumentException("At least one reading-state field must be supplied.", nameof(readingStatus));
        }

        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 256)
        {
            throw new ArgumentException("Reason exceeds the history contract limit.", nameof(reason));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        BookRow book = await context.Books
            .FirstOrDefaultAsync(candidate => candidate.BookId == bookId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Book '{bookId}' was not found.");

        ReadingProgressRow? progress = null;
        if (readingStatus is not null)
        {
            progress = await context.ReadingProgress
                .FirstOrDefaultAsync(candidate => candidate.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);
            if (progress is null)
            {
                progress = new ReadingProgressRow
                {
                    BookId = bookId,
                    Status = (int)ReadingStatus.Unread,
                };
                context.ReadingProgress.Add(progress);
            }

            progress.Status = (int)readingStatus.Value;
        }

        if (rating is not null)
        {
            book.Rating = rating;
        }

        if (isFavourite is not null)
        {
            book.IsFavourite = isFavourite.Value;
        }

        context.ReadingStateHistory.Add(new ReadingStateHistoryRow
        {
            BookId = bookId,
            ReadingStatus = progress is null ? null : (ReadingStatus)progress.Status,
            Rating = book.Rating,
            IsFavourite = book.IsFavourite,
            Reason = reason.Trim(),
            ChangedUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadingStateHistoryEntry>> GetHistoryAsync(
        string bookId,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        if (maxResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "History results must be between 1 and 100.");
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        // SQLite cannot translate DateTimeOffset ORDER BY expressions. The
        // auto-increment history key is assigned at insert time, so it provides
        // a deterministic newest-first page while the original UTC timestamp is
        // still returned for presentation.
        return await lease.Context.ReadingStateHistory
            .AsNoTracking()
            .Where(row => row.BookId == bookId)
            .OrderByDescending(row => row.ReadingStateHistoryId)
            .Take(maxResults)
            .Select(row => new ReadingStateHistoryEntry(
                row.ReadingStatus,
                row.Rating,
                row.IsFavourite,
                row.Reason,
                row.ChangedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
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
