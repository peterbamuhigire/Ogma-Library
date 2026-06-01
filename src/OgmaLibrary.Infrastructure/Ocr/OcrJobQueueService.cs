using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

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

        string? filePath = ResolveFilePath(book);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new OcrQueueResult(false, false, null, "No available PDF file was found for OCR.");
        }

        string payload = JsonSerializer.Serialize(new
        {
            FilePath = filePath,
            Language = languageHint,
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
            IdempotencyKey = ComputeIdempotencyKey(bookId, languageHint),
            Status = 0,
            Payload = payload,
        };
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OcrQueueResult(true, false, job.JobId, null);
    }

    private string? ResolveFilePath(BookRow book)
    {
        string? relativePath = book.BookFiles
            .Where(file => file.FileStatus == 0)
            .OrderBy(file => file.BookFileId)
            .Select(file => file.RelativePath)
            .FirstOrDefault();
        relativePath ??= book.RelativePath;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(_libraryRoot, normalized));

        if (!IsUnderLibraryRoot(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        return fullPath;
    }

    private bool IsUnderLibraryRoot(string fullPath)
    {
        string root = _libraryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _libraryRoot
            : _libraryRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
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
