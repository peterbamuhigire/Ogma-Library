using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Flags BookFiles no longer present on disk as Missing (FileStatus=1) and sets the
/// owning Book's Status to Unavailable (1), while leaving all user data
/// (annotations, reading progress, bookmarks) intact (FR-LIB-004, reversibility R1).
/// Each flagged file produces an <c>AuditEvent</c> with <c>EventType = "BookMarkedUnavailable"</c>.
/// </summary>
public sealed class UnavailableFileFlagService : IUnavailableFileFlagService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="UnavailableFileFlagService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public UnavailableFileFlagService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<int> FlagMissingFilesAsync(
        string libraryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        // Load all Present book files (FileStatus=0).
        List<BookFileRow> presentFiles = await _context.BookFiles
            .Where(f => f.FileStatus == 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(libraryRoot));
        int flagged = 0;

        foreach (BookFileRow fileRow in presentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string absolutePath = Path.Combine(
                normalizedRoot,
                fileRow.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(absolutePath))
            {
                continue;
            }

            // Flag the file as missing.
            fileRow.FileStatus = 1; // Missing

            // Flag the owning book as Unavailable (only if currently Active).
            BookRow? book = await _context.Books
                .FirstOrDefaultAsync(b => b.BookId == fileRow.BookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null && book.Status == 0) // Active
            {
                book.Status = 1; // Unavailable
            }

            // Append audit event — never deleted (NFR-PROD-013).
            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "BookMarkedUnavailable",
                EntityId = fileRow.BookId,
                EntityType = "Book",
                AfterJson = $"{{\"relativePath\":\"{fileRow.RelativePath}\"}}",
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });

            flagged++;
        }

        if (flagged > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return flagged;
    }
}
