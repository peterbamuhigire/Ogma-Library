using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// EF Core implementation of <see cref="ILibraryHealthService"/> that aggregates all
/// five health sections via concurrent targeted queries (FR-META-007,
/// power-librarian persona, NFR-PROD-003 &lt; 500 ms).
/// </summary>
public sealed class LibraryHealthService : ILibraryHealthService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="LibraryHealthService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public LibraryHealthService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<LibraryHealthSnapshot> GetHealthSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        // Run all five queries concurrently for maximum throughput.
        var duplicatesTask = LoadDuplicatesAsync(cancellationToken);
        var missingCoversTask = LoadMissingCoversAsync(cancellationToken);
        var missingIsbnsTask = LoadMissingIsbnsAsync(cancellationToken);
        var unavailableTask = LoadUnavailableFilesAsync(cancellationToken);
        var failedJobsTask = LoadFailedJobsAsync(cancellationToken);

        await Task.WhenAll(duplicatesTask, missingCoversTask, missingIsbnsTask,
            unavailableTask, failedJobsTask).ConfigureAwait(false);

        return new LibraryHealthSnapshot(
            Duplicates: await duplicatesTask.ConfigureAwait(false),
            MissingCovers: await missingCoversTask.ConfigureAwait(false),
            MissingIsbns: await missingIsbnsTask.ConfigureAwait(false),
            UnavailableFiles: await unavailableTask.ConfigureAwait(false),
            FailedJobs: await failedJobsTask.ConfigureAwait(false),
            LoadedUtc: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return;
        }

        job.Status = 0; // Pending
        job.ErrorMessage = null;
        job.CompletedUtc = null;
        job.RetryCount++;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<DuplicateBookEntry>> LoadDuplicatesAsync(
        CancellationToken cancellationToken)
    {
        // Load all books with an ISBN or hash; group client-side for SQLite compatibility.
        var booksWithIsbn = await _context.Books
            .AsNoTracking()
            .Where(b => b.IsbnNormalized != null)
            .Select(b => new { b.BookId, b.Title, b.IsbnNormalized, b.Sha256Hash })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<DuplicateBookEntry>();

        // ISBN duplicates.
        var isbnGroups = booksWithIsbn
            .GroupBy(b => b.IsbnNormalized!)
            .Where(g => g.Count() > 1);

        foreach (var g in isbnGroups)
        {
            foreach (var b in g)
            {
                result.Add(new DuplicateBookEntry(b.BookId, b.Title, g.Key, "ISBN"));
            }
        }

        // Hash duplicates.
        var booksWithHash = await _context.Books
            .AsNoTracking()
            .Where(b => b.Sha256Hash != null)
            .Select(b => new { b.BookId, b.Title, b.Sha256Hash })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hashGroups = booksWithHash
            .GroupBy(b => b.Sha256Hash!)
            .Where(g => g.Count() > 1);

        var seenIds = new HashSet<string>(result.Select(r => r.BookId), StringComparer.OrdinalIgnoreCase);

        foreach (var g in hashGroups)
        {
            foreach (var b in g)
            {
                // Only add if not already in result (a book may duplicate on both ISBN and hash).
                if (seenIds.Add($"{b.BookId}|ContentHash"))
                {
                    result.Add(new DuplicateBookEntry(b.BookId, b.Title, g.Key, "ContentHash"));
                }
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<MissingCoverEntry>> LoadMissingCoversAsync(
        CancellationToken cancellationToken)
    {
        // Books with no Cover metadata field value set.
        var missing = await _context.Books
            .AsNoTracking()
            .Where(b => !b.MetadataFields.Any(
                f => f.FieldName == "Cover" && f.Value != null))
            .OrderBy(b => b.Title ?? string.Empty)
            .Select(b => new { b.BookId, b.Title })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return missing.Select(m => new MissingCoverEntry(m.BookId, m.Title)).ToList();
    }

    private async Task<IReadOnlyList<MissingIsbnEntry>> LoadMissingIsbnsAsync(
        CancellationToken cancellationToken)
    {
        var missing = await _context.Books
            .AsNoTracking()
            .Where(b => b.IsbnNormalized == null)
            .OrderBy(b => b.Title ?? string.Empty)
            .Select(b => new { b.BookId, b.Title })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return missing.Select(m => new MissingIsbnEntry(m.BookId, m.Title)).ToList();
    }

    private async Task<IReadOnlyList<UnavailableFileEntry>> LoadUnavailableFilesAsync(
        CancellationToken cancellationToken)
    {
        // Books whose status is Unavailable (1).
        var unavailable = await _context.Books
            .AsNoTracking()
            .Where(b => b.Status == 1)
            .OrderBy(b => b.Title ?? string.Empty)
            .Select(b => new { b.BookId, b.Title, b.RelativePath })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return unavailable.Select(u => new UnavailableFileEntry(u.BookId, u.Title, u.RelativePath)).ToList();
    }

    private async Task<IReadOnlyList<FailedJobEntry>> LoadFailedJobsAsync(
        CancellationToken cancellationToken)
    {
        // Jobs with status = Failed (3).
        var failed = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == 3)
            .OrderByDescending(j => j.JobId)
            .Select(j => new
            {
                j.JobId,
                j.JobType,
                j.BookId,
                j.ErrorMessage,
                j.CompletedUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return failed.Select(f => new FailedJobEntry(
            f.JobId,
            f.JobType,
            f.BookId,
            f.ErrorMessage,
            f.CompletedUtc)).ToList();
    }
}
