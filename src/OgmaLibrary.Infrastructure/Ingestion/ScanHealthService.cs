using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Aggregates scan health data from the Jobs table and Books catalogue for the V1
/// scan health report panel (FR-LIB-007). Groups failures into four actionable
/// categories: general failures, password-protected, missing thumbnails, and metadata gaps.
/// </summary>
public sealed class ScanHealthService : IScanHealthService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ScanHealthService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal ScanHealthService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ScanHealthService"/>.
    /// </summary>
    public ScanHealthService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<ScanHealthReport> GetReportAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // General failed jobs (excluding password-protected sentinel).
        // Use JobId for ORDER BY since SQLite does not support DateTimeOffset in ORDER BY.
        List<JobRow> failedJobs = await context.Jobs
            .Where(j => j.Status == 3 && j.JobType != "PasswordProtectedDetected")
            .OrderByDescending(j => j.JobId)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Password-protected files (sentinel job type).
        List<JobRow> passwordJobs = await context.Jobs
            .Where(j => j.JobType == "PasswordProtectedDetected")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Books missing thumbnails: ThumbnailGeneration jobs that failed or are still pending.
        List<JobRow> missingThumbnailJobs = await context.Jobs
            .Where(j => j.JobType == "ThumbnailGeneration" &&
                        (j.Status == 3 || j.Status == 0))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Books with metadata gaps (no Title in BookMetadataFields).
        List<string> booksWithTitle = await context.BookMetadataFields
            .Where(f => f.FieldName == "Title" && f.Value != null)
            .Select(f => f.BookId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<string> allActiveBookIds = await context.Books
            .Where(b => b.Status == 0) // Active
            .Select(b => b.BookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var metadataGapIds = allActiveBookIds.Except(booksWithTitle).Take(200).ToList();
        var metadataGapItems = new List<ScanFailureItem>();

        foreach (string bookId in metadataGapIds)
        {
            metadataGapItems.Add(new ScanFailureItem(
                SourceReference: $"book:{bookId}",
                FailureCode: "missing_title",
                JobId: 0,
                FailedAtUtc: DateTimeOffset.UtcNow));
        }

        return new ScanHealthReport(
            FailedJobs: failedJobs.Select(j => new ScanFailureItem(
                SourceReference: GetSourceReference(j),
                FailureCode: GetFailureCode(j, "job_failed"),
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            PasswordProtected: passwordJobs.Select(j => new ScanFailureItem(
                SourceReference: GetSourceReference(j),
                FailureCode: "password_protected",
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            MissingThumbnails: missingThumbnailJobs.Select(j => new ScanFailureItem(
                SourceReference: GetSourceReference(j),
                FailureCode: j.Status == (int)JobRuntimeStatus.Pending
                    ? "thumbnail_pending"
                    : GetFailureCode(j, "thumbnail_failed"),
                JobId: j.JobId,
                FailedAtUtc: j.CompletedUtc ?? DateTimeOffset.UtcNow)).ToList(),
            MetadataGaps: metadataGapItems);
    }

    private static string GetSourceReference(JobRow job) =>
        string.IsNullOrWhiteSpace(job.BookId)
            ? $"job:{job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"book:{job.BookId}";

    private static string GetFailureCode(JobRow job, string fallback) =>
        string.IsNullOrWhiteSpace(job.FailureCode) ? fallback : job.FailureCode;

    /// <inheritdoc />
    public async Task RetryAllFailedAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<JobRow> failed = await context.Jobs
            .Where(j => j.Status == 3)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (JobRow job in failed)
        {
            PrepareRetry(job);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        JobRow? job = await context.Jobs
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.JobId == jobId &&
                    candidate.Status == (int)JobRuntimeStatus.Failed,
                cancellationToken)
            .ConfigureAwait(false);

        if (job is not null)
        {
            PrepareRetry(job);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void PrepareRetry(JobRow job)
    {
        job.Status = (int)JobRuntimeStatus.Pending;
        job.RetryCount += 1;
        job.StartedUtc = null;
        job.CompletedUtc = null;
        job.LeaseOwner = null;
        job.LeaseExpiresUtc = null;
        job.NextAttemptUtc = DateTimeOffset.UtcNow;
        job.FailureCode = null;
        job.ErrorMessage = null;
    }
}
