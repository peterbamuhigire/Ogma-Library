using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Relinks a catalogued book to a PDF the user located (Sept-23 Phase 11, task 11.7). The
/// candidate path is untrusted: it must resolve, symbolic links included, inside an enabled
/// library root through <see cref="PathGuard"/>, and its SHA-256 must equal the catalogued hash.
/// The file row, the book's availability and an audit event are written in one save.
/// </summary>
public sealed class BookRelinkService : IBookRelinkService
{
    /// <summary>Audit event type recorded for every successful relink.</summary>
    public const string AuditEventType = "BookFileRelinked";

    private const int PresentFileStatus = 0;
    private const int UnavailableBookStatus = 1;
    private const int ActiveBookStatus = 0;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _db;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance over one context (tests).</summary>
    /// <param name="db">The catalogue database context.</param>
    internal BookRelinkService(CatalogueDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _logger = NullLogger.Instance;
    }

    /// <summary>Initializes a new instance of <see cref="BookRelinkService"/>.</summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="logger">The redacting logger; paths are never logged.</param>
    [ActivatorUtilitiesConstructor]
    public BookRelinkService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILogger<BookRelinkService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<BookRelinkResult> RelinkAsync(
        string bookId,
        string candidatePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        if (string.IsNullOrWhiteSpace(candidatePath) || !IsFullyQualified(candidatePath))
        {
            return BookRelinkResult.OutsideLibraryRoots;
        }

        CatalogueDbContext context = _db ?? await _contextFactory!
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await RelinkCoreAsync(context, bookId, candidatePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            InfrastructureLog.BookRelinkFailed(_logger, ex);
            return BookRelinkResult.Failed;
        }
        finally
        {
            if (_db is null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<BookRelinkResult> RelinkCoreAsync(
        CatalogueDbContext context,
        string bookId,
        string candidatePath,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LibraryRootLocation> roots = await LibraryRootPaths
            .GetActiveRootsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        (LibraryRootLocation Root, string CanonicalRoot, string CanonicalPath)? placed = Place(roots, candidatePath);
        if (placed is not { } location)
        {
            return BookRelinkResult.OutsideLibraryRoots;
        }

        if (!File.Exists(location.CanonicalPath))
        {
            return BookRelinkResult.NotFound;
        }

        BookRow? book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return BookRelinkResult.NotFound;
        }

        if (string.IsNullOrWhiteSpace(book.Sha256Hash))
        {
            // Without a catalogued hash the identity of the chosen file cannot be verified.
            return BookRelinkResult.Failed;
        }

        string hash = await ComputeSha256Async(location.CanonicalPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(hash, book.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            return BookRelinkResult.HashMismatch;
        }

        string relativePath = LibraryRootPaths.ToRelativePath(location.CanonicalRoot, location.CanonicalPath);
        List<BookFileRow> files = await context.BookFiles
            .Where(f => f.BookId == bookId)
            .OrderBy(f => f.BookFileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Same preference as a rescan move: the row already at this path, then a missing row.
        BookFileRow? file =
            files.FirstOrDefault(f =>
                f.LibraryRootId == location.Root.RootId &&
                string.Equals(f.RelativePath, relativePath, StringComparison.Ordinal)) ??
            files.FirstOrDefault(f => f.FileStatus != PresentFileStatus) ??
            files.FirstOrDefault();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (file is null)
        {
            file = new BookFileRow { BookId = bookId };
            context.BookFiles.Add(file);
        }

        file.LibraryRootId = location.Root.RootId;
        file.RelativePath = relativePath;
        file.FileStatus = PresentFileStatus;
        file.LastSeenUtc = now;
        if (book.Status == UnavailableBookStatus)
        {
            book.Status = ActiveBookStatus;
        }

        // The audit records identifiers only; the private path never leaves the file row.
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = AuditEventType,
            EntityId = bookId,
            EntityType = "Book",
            AfterJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["libraryRootId"] = location.Root.RootId,
                ["source"] = "inspector",
            }),
            Timestamp = now,
            IsLocalOnly = true,
        });

        // One SaveChanges is one SQLite transaction: file row, book status and audit together.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BookRelinkResult.Relinked;
    }

    private static (LibraryRootLocation Root, string CanonicalRoot, string CanonicalPath)? Place(
        IReadOnlyList<LibraryRootLocation> roots,
        string candidatePath)
    {
        foreach (LibraryRootLocation root in roots.OrderByDescending(r => r.CanonicalPath.Length))
        {
            try
            {
                string canonicalRoot = PathGuard.CanonicalizeRoot(root.CanonicalPath);
                string canonicalPath = PathGuard.EnsureWithinRoot(candidatePath, canonicalRoot);
                return (root, canonicalRoot, canonicalPath);
            }
            catch (Exception ex) when (ex is PathTraversalException or ArgumentException or IOException or NotSupportedException)
            {
                // Intentionally ignored: not inside this root (or unresolvable); try the next enabled root.
            }
        }

        return null;
    }

    private static bool IsFullyQualified(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
    }
}
