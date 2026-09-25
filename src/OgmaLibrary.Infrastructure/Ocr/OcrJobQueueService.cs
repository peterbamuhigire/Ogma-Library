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
        if (existing is { Status: 0 or 1 or 2 or 5 })
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

        if (existing is { Status: 3 or 4 })
        {
            existing.Status = 0;
            existing.Payload = payload;
            existing.StartedUtc = null;
            existing.CompletedUtc = null;
            existing.ErrorMessage = null;
            existing.RetryCount += 1;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
        return new OcrQueueResult(true, false, job.JobId, null);
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
