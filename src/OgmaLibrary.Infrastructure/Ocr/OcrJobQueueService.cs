using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>EF-backed OCR job queue for Phase 15 triggers.</summary>
public sealed class OcrJobQueueService : IOcrJobQueueService
{
    private const string OcrJobType = "OcrJob";
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly string _libraryRoot;

    /// <summary>Initializes a queue service from the app composition root.</summary>
    [ActivatorUtilitiesConstructor]
    public OcrJobQueueService(IDbContextFactory<CatalogueDbContext> contextFactory, string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        _contextFactory = contextFactory;
        _libraryRoot = Path.GetFullPath(libraryRoot);
    }

    /// <summary>Initializes a queue service for tests sharing one context.</summary>
    internal OcrJobQueueService(CatalogueDbContext context, string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        _context = context;
        _libraryRoot = Path.GetFullPath(libraryRoot);
    }

    /// <inheritdoc />
    public async Task<OcrQueueResult> QueueBookAsync(
        string bookId,
        string languageHint = "eng",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageHint);
        string? normalizedLanguage = OcrLanguagePolicy.Normalize(languageHint);
        if (normalizedLanguage is null)
        {
            return new OcrQueueResult(false, false, null, "Unsupported OCR language pack.");
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        BookRow? book = await context.Books
            .Include(row => row.BookFiles)
            .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return new OcrQueueResult(false, false, null, "Book not found.");
        }

        JobRow? existing = await context.Jobs
            .Where(job => job.JobType == OcrJobType && job.BookId == bookId)
            .OrderByDescending(job => job.JobId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        // Pending, running, completed, paused or waiting: nothing to queue. Failed, cancelled
        // and dead-lettered jobs are queued again (Sept-23 Phase 17; a paused job used to get a
        // duplicate row that collided with the idempotency key).
        if (existing is { Status: 0 or 1 or 2 or 6 or 7 })
        {
            return new OcrQueueResult(false, true, existing.JobId, null);
        }

        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        string? filePath = ResolveFilePath(book, roots);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new OcrQueueResult(false, false, null, "No available PDF file was found for OCR.");
        }

        string payload = JsonSerializer.Serialize(new
        {
            FilePath = filePath,
            Language = normalizedLanguage,
            TotalPages = 0,
            ProcessedPages = 0,
        });

        if (existing is { Status: 3 or 4 or 5 })
        {
            // A user request starts a fresh set of attempts; the requeue is counted apart
            // (Sept-23 Phase 06, T06.2).
            existing.Status = 0;
            existing.Payload = payload;
            existing.StartedUtc = null;
            existing.CompletedUtc = null;
            existing.NextAttemptUtc = null;
            existing.ErrorMessage = null;
            existing.FailureCode = null;
            existing.RetryCount = 0;
            existing.RequeueCount += 1;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await MarkInProgressAsync(context, bookId, cancellationToken).ConfigureAwait(false);
            return new OcrQueueResult(true, false, existing.JobId, null);
        }

        var job = new JobRow
        {
            BookId = bookId,
            JobType = OcrJobType,
            IdempotencyKey = ComputeIdempotencyKey(bookId, normalizedLanguage),
            Status = 0,
            Payload = payload,
        };
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await MarkInProgressAsync(context, bookId, cancellationToken).ConfigureAwait(false);
        return new OcrQueueResult(true, false, job.JobId, null);
    }

    /// <inheritdoc />
    public async Task<int> CountBooksNeedingOcrAsync(CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await NeedingOcr(lease.Context).CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> QueueBooksNeedingOcrAsync(
        string languageHint = "eng",
        int maxBooks = 500,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBooks);
        List<string> bookIds;
        using (ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false))
        {
            bookIds = await NeedingOcr(lease.Context)
                .OrderBy(book => book.BookId)
                .Select(book => book.BookId)
                .Take(maxBooks)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int queued = 0;
        foreach (string bookId in bookIds)
        {
            OcrQueueResult result = await QueueBookAsync(bookId, languageHint, cancellationToken).ConfigureAwait(false);
            if (result.Queued)
            {
                queued++;
            }
        }

        return queued;
    }

    /// <summary>
    /// Active, readable books whose text status says OCR would help (image only, partly
    /// searchable, or a failed OCR attempt) and that have no OCR job in flight.
    /// </summary>
    internal static IQueryable<BookRow> NeedingOcr(CatalogueDbContext context) =>
        context.Books
            .AsNoTracking()
            .Where(book => book.Status == 0 &&
                !book.IsPasswordProtected &&
                (book.TextStatus == (int)BookTextStatus.ImageOnly ||
                 book.TextStatus == (int)BookTextStatus.PartlySearchable ||
                 book.TextStatus == (int)BookTextStatus.OcrFailed) &&
                !context.Jobs.Any(job => job.JobType == OcrJobType &&
                    job.BookId == book.BookId &&
                    (job.Status == 0 || job.Status == 1 || job.Status == 6 || job.Status == 7)));

    private static async Task MarkInProgressAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        await context.Books
            .Where(book => book.BookId == bookId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(book => book.TextStatus, (int)BookTextStatus.OcrInProgress),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private string? ResolveFilePath(BookRow book, IReadOnlyDictionary<string, LibraryRootLocation> roots)
    {
        // Sept-23 Phase 05: each file resolves through its own root (bounded by
        // PathGuard); the configured root is only the fallback for legacy rows.
        foreach (BookFileRow file in book.BookFiles
                     .Where(file => file.FileStatus == 0)
                     .OrderBy(file => file.BookFileId))
        {
            string? path = LibraryRootPaths.Resolve(roots, file, _libraryRoot);
            if (path is not null && File.Exists(path))
            {
                return path;
            }
        }

        if (string.IsNullOrWhiteSpace(book.RelativePath))
        {
            return null;
        }

        string? legacyPath = LibraryRootPaths.Resolve(roots, null, book.RelativePath, _libraryRoot);
        return legacyPath is not null && File.Exists(legacyPath) ? legacyPath : null;
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

    private static string ComputeIdempotencyKey(string bookId, string languageHint)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{OcrJobType}|{languageHint}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
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
