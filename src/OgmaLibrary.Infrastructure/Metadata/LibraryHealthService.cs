using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// EF Core implementation of <see cref="ILibraryHealthService"/> that aggregates all
/// five health sections via concurrent targeted queries (FR-META-007,
/// power-librarian persona, NFR-PROD-003 &lt; 500 ms).
/// </summary>
public sealed class LibraryHealthService : ILibraryHealthService
{
    private static readonly int[] PausableBatchStatuses = [0, 1];
    private static readonly int[] ResumableBatchStatuses = [3, 5];

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="LibraryHealthService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal LibraryHealthService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LibraryHealthService"/>.
    /// </summary>
    public LibraryHealthService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<LibraryHealthSnapshot> GetHealthSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (_contextFactory is null)
        {
            return new LibraryHealthSnapshot(
                Duplicates: await LoadDuplicatesAsync(cancellationToken).ConfigureAwait(false),
                MissingCovers: await LoadMissingCoversAsync(cancellationToken).ConfigureAwait(false),
                MissingIsbns: await LoadMissingIsbnsAsync(cancellationToken).ConfigureAwait(false),
                UnavailableFiles: await LoadUnavailableFilesAsync(cancellationToken).ConfigureAwait(false),
                FailedJobs: await LoadFailedJobsAsync(cancellationToken).ConfigureAwait(false),
                LoadedUtc: DateTimeOffset.UtcNow,
                BatchEnrichmentRuns: await LoadBatchRunsAsync(cancellationToken).ConfigureAwait(false));
        }

        // Run all five queries concurrently for maximum throughput.
        var duplicatesTask = LoadDuplicatesAsync(cancellationToken);
        var missingCoversTask = LoadMissingCoversAsync(cancellationToken);
        var missingIsbnsTask = LoadMissingIsbnsAsync(cancellationToken);
        var unavailableTask = LoadUnavailableFilesAsync(cancellationToken);
        var failedJobsTask = LoadFailedJobsAsync(cancellationToken);
        var batchRunsTask = LoadBatchRunsAsync(cancellationToken);

        await Task.WhenAll(duplicatesTask, missingCoversTask, missingIsbnsTask,
            unavailableTask, failedJobsTask, batchRunsTask).ConfigureAwait(false);

        return new LibraryHealthSnapshot(
            Duplicates: await duplicatesTask.ConfigureAwait(false),
            MissingCovers: await missingCoversTask.ConfigureAwait(false),
            MissingIsbns: await missingIsbnsTask.ConfigureAwait(false),
            UnavailableFiles: await unavailableTask.ConfigureAwait(false),
            FailedJobs: await failedJobsTask.ConfigureAwait(false),
            LoadedUtc: DateTimeOffset.UtcNow,
            BatchEnrichmentRuns: await batchRunsTask.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var job = await context.Jobs
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task PauseBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default) =>
        UpdateBatchJobsAsync(
            batchId,
            statuses: PausableBatchStatuses,
            update: job =>
            {
                job.Status = 5;
                job.StartedUtc = null;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ResumeBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default) =>
        UpdateBatchJobsAsync(
            batchId,
            statuses: ResumableBatchStatuses,
            update: job =>
            {
                if (job.Status == 3)
                {
                    job.RetryCount++;
                }

                job.Status = 0;
                job.StartedUtc = null;
                job.CompletedUtc = null;
                job.ErrorMessage = null;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<string> ExportFailedJobsCsvAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FailedJobEntry> failedJobs = await LoadFailedJobsAsync(cancellationToken)
            .ConfigureAwait(false);
        var csv = new StringBuilder();
        csv.AppendLine("JobId,JobType,BookId,ErrorMessage,FailedUtc");
        foreach (FailedJobEntry job in failedJobs)
        {
            csv
                .Append(job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(job.JobType)).Append(',')
                .Append(EscapeCsv(job.BookId)).Append(',')
                .Append(EscapeCsv(job.ErrorMessage)).Append(',')
                .Append(EscapeCsv(job.FailedUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return csv.ToString();
    }

    private async Task<IReadOnlyList<DuplicateBookEntry>> LoadDuplicatesAsync(
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Load all books with an ISBN or hash; group client-side for SQLite compatibility.
        var booksWithIsbn = await context.Books
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
        var booksWithHash = await context.Books
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
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Books with no Cover metadata field value set.
        var missing = await context.Books
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
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var missing = await context.Books
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
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Books whose status is Unavailable (1).
        var unavailable = await context.Books
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
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Jobs with status = Failed (3).
        var failed = await context.Jobs
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

    private async Task<IReadOnlyList<BatchEnrichmentRunEntry>> LoadBatchRunsAsync(
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<BatchJobSnapshot> jobs = await LoadBatchJobSnapshotsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        return jobs
            .GroupBy(job => job.BatchId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new BatchEnrichmentRunEntry(
                group.Key,
                TotalJobs: group.Count(),
                PendingJobs: group.Count(job => job.Status == 0),
                RunningJobs: group.Count(job => job.Status == 1),
                CompletedJobs: group.Count(job => job.Status == 2),
                FailedJobs: group.Count(job => job.Status == 3),
                PausedJobs: group.Count(job => job.Status == 5)))
            .ToList();
    }

    private async Task UpdateBatchJobsAsync(
        string batchId,
        IReadOnlyCollection<int> statuses,
        Action<JobRow> update,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<JobRow> jobs = await context.Jobs
            .Where(job => job.JobType == "Enrich" && statuses.Contains(job.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (JobRow job in jobs.Where(job => string.Equals(TryReadBatchId(job.Payload), batchId, StringComparison.Ordinal)))
        {
            update(job);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<BatchJobSnapshot>> LoadBatchJobSnapshotsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        var rows = await context.Jobs
            .AsNoTracking()
            .Where(job => job.JobType == "Enrich" && job.Payload != null && EF.Functions.Like(job.Payload, "{%"))
            .Select(job => new { job.JobId, job.Status, job.Payload })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var jobs = new List<BatchJobSnapshot>();
        foreach (var row in rows)
        {
            string? batchId = TryReadBatchId(row.Payload);
            if (!string.IsNullOrWhiteSpace(batchId))
            {
                jobs.Add(new BatchJobSnapshot(row.JobId, batchId, row.Status));
            }
        }

        return jobs;
    }

    private static string? TryReadBatchId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BatchEnrichmentJobPayload>(payload)?.BatchId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Any(ch => ch is ',' or '"' or '\r' or '\n')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private sealed record BatchJobSnapshot(long JobId, string BatchId, int Status);
}
