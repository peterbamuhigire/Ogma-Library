using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Inserts new <c>Book</c> and <c>BookFile</c> rows, enqueues metadata, thumbnail,
/// spine, and search jobs with idempotency keys, and updates file paths for
/// re-matched books (FR-LIB-003, FR-LIB-005, NFR-OGMA-009).
/// </summary>
public sealed class BookRegistrationService : IBookRegistrationService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookRegistrationService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal BookRegistrationService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BookRegistrationService"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="serviceProvider">The application service provider, used only to make DI constructor selection unambiguous.</param>
    [ActivatorUtilitiesConstructor]
    public BookRegistrationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<string> RegisterAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string bookId = CanonicalIdGenerator.NewId();

        context.Books.Add(new BookRow
        {
            BookId = bookId,
            Sha256Hash = contentHash,
            SizeBytes = discovered.SizeBytes,
            MtimeTicks = discovered.MtimeTicks,
            Status = 0, // Active
            IsPasswordProtected = discovered.Validity == FileValidity.Locked,
        });

        context.BookFiles.Add(new BookFileRow
        {
            BookId = bookId,
            LibraryRootId = discovered.LibraryRootId,
            RelativePath = discovered.RelativePath,
            FileStatus = 0, // Present
            FileValidity = (int)discovered.Validity,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });

        // Enqueue metadata extraction job (idempotent).
        TryAddJob(context, bookId, "MetadataExtraction",
            ComputeIdempotencyKey(bookId, "MetadataExtraction", contentHash), discovered.AbsolutePath);

        // Enqueue thumbnail generation job (idempotent).
        TryAddJob(context, bookId, "ThumbnailGeneration",
            ComputeIdempotencyKey(bookId, "ThumbnailGeneration", contentHash), discovered.AbsolutePath);

        // Enqueue spine generation job (idempotent).
        TryAddJob(context, bookId, "SpineGeneration",
            ComputeIdempotencyKey(bookId, "SpineGeneration", contentHash), discovered.AbsolutePath);

        TryAddJob(context, bookId, "SearchExtraction",
            ComputeIdempotencyKey(bookId, "SearchExtraction", contentHash), discovered.AbsolutePath);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bookId;
    }

    /// <inheritdoc />
    public async Task UpdateFilePathAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<BookFileRow> fileRows = await context.BookFiles
            .Where(f => f.BookId == bookId)
            .OrderBy(f => f.BookFileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Sept-23 Phase 05: prefer the row already at this root and path, then a row
        // whose file went missing (a move), and only then the book's first row.
        BookFileRow? fileRow =
            fileRows.FirstOrDefault(f =>
                f.LibraryRootId == discovered.LibraryRootId &&
                string.Equals(f.RelativePath, discovered.RelativePath, StringComparison.Ordinal)) ??
            fileRows.FirstOrDefault(f => f.FileStatus != 0) ??
            fileRows.FirstOrDefault();

        if (fileRow is not null)
        {
            fileRow.LibraryRootId = discovered.LibraryRootId;
            fileRow.RelativePath = discovered.RelativePath;
            fileRow.FileStatus = 0; // Present
            fileRow.FileValidity = (int)discovered.Validity;
            fileRow.LastSeenUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            context.BookFiles.Add(new BookFileRow
            {
                BookId = bookId,
                LibraryRootId = discovered.LibraryRootId,
                RelativePath = discovered.RelativePath,
                FileStatus = 0,
                FileValidity = (int)discovered.Validity,
                LastSeenUtc = DateTimeOffset.UtcNow,
            });
        }

        // Re-activate the book if it was flagged as Unavailable.
        BookRow? book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is not null)
        {
            if (book.Status == 1) // Unavailable
            {
                book.Status = 0; // Active
            }

            if (discovered.Validity == FileValidity.Locked)
            {
                book.IsPasswordProtected = true;
            }

            // Update identity attributes.
            book.Sha256Hash = contentHash;
            book.SizeBytes = discovered.SizeBytes;
            book.MtimeTicks = discovered.MtimeTicks;
        }

        TryAddJob(context, bookId, "MetadataExtraction",
            ComputeIdempotencyKey(bookId, "MetadataExtraction", contentHash), discovered.AbsolutePath);

        TryAddJob(context, bookId, "ThumbnailGeneration",
            ComputeIdempotencyKey(bookId, "ThumbnailGeneration", contentHash), discovered.AbsolutePath);

        TryAddJob(context, bookId, "SpineGeneration",
            ComputeIdempotencyKey(bookId, "SpineGeneration", contentHash), discovered.AbsolutePath);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryAddJob(
        CatalogueDbContext context,
        string bookId,
        string jobType,
        string idempotencyKey,
        string filePath)
    {
        JobRow? existing = context.Jobs.FirstOrDefault(j => j.IdempotencyKey == idempotencyKey);
        if (existing is null)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = jobType,
                IdempotencyKey = idempotencyKey,
                Status = 0, // Pending
                BookId = bookId,
                Payload = filePath,
            });
            return;
        }

        if (existing.Status is 3 or 4 or 5)
        {
            // Re-registration is new work, not another attempt (Sept-23 Phase 06, T06.2).
            existing.Status = 0; // Pending
            existing.Payload = filePath;
            existing.StartedUtc = null;
            existing.CompletedUtc = null;
            existing.ErrorMessage = null;
            existing.FailureCode = null;
            existing.NextAttemptUtc = null;
            existing.RetryCount = 0;
            existing.RequeueCount += 1;
        }
    }

    private static string ComputeIdempotencyKey(string bookId, string jobType, string contentHash)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}|{contentHash}");
        byte[] hash = SHA256.HashData(data);
        // 32-hex-char idempotency key (128-bit) is sufficient for uniqueness.
        return Convert.ToHexStringLower(hash)[..32];
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
