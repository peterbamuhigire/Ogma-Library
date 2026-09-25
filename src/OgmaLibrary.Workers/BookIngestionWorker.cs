using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker that processes pending background jobs (MetadataExtraction,
/// ThumbnailGeneration, SpineGeneration, Enrich) from the Jobs queue (NFR-OGMA-009,
/// NFR-PROD-005). Per-file failure isolation: one failing job never cancels siblings.
/// Sept-23 Phase 06: the loop is guarded (T06.5), shutdown releases the lease without
/// consuming an attempt (T06.2), a book's jobs run as one batch against one sandboxed worker
/// session (T06.10), and progress goes to the processing snapshot, never the scan's file
/// counters (T06.6, K13).
/// </summary>
public sealed class BookIngestionWorker : BackgroundService
{
    /// <summary>The job types this worker executes.</summary>
    internal static readonly string[] JobTypes = ["MetadataExtraction", "Enrich", "ThumbnailGeneration", "SpineGeneration"];

    private const int MaxJobsPerBookBatch = 8;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IJobRuntimeService _jobRuntime;
    private readonly IMetadataExtractionService _metadataExtraction;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISpineService _spineService;
    private readonly IBookMetadataEnrichmentService _metadataEnrichment;
    private readonly IProcessingProgressService? _processingProgress;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIngestionWorker"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="jobRuntime">The durable lease runtime.</param>
    /// <param name="metadataExtraction">The metadata extraction service.</param>
    /// <param name="thumbnailService">The thumbnail generation service.</param>
    /// <param name="spineService">The spine generation service.</param>
    /// <param name="metadataEnrichment">The deterministic online metadata enrichment service.</param>
    /// <param name="processingProgress">Optional processing progress publisher (Sept-23 Phase 06).</param>
    /// <param name="logger">Optional logger.</param>
    public BookIngestionWorker(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IJobRuntimeService jobRuntime,
        IMetadataExtractionService metadataExtraction,
        IThumbnailService thumbnailService,
        ISpineService spineService,
        IBookMetadataEnrichmentService metadataEnrichment,
        IProcessingProgressService? processingProgress = null,
        ILogger<BookIngestionWorker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(jobRuntime);
        ArgumentNullException.ThrowIfNull(metadataExtraction);
        ArgumentNullException.ThrowIfNull(thumbnailService);
        ArgumentNullException.ThrowIfNull(spineService);
        ArgumentNullException.ThrowIfNull(metadataEnrichment);

        _contextFactory = contextFactory;
        _jobRuntime = jobRuntime;
        _metadataExtraction = metadataExtraction;
        _thumbnailService = thumbnailService;
        _spineService = spineService;
        _metadataEnrichment = metadataEnrichment;
        _processingProgress = processingProgress;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ResilientJobLoop.RunAsync(
            nameof(BookIngestionWorker),
            RunOnceAsync,
            IdleDelay,
            _logger,
            stoppingToken);

    /// <summary>
    /// Claims one job and then the book's other due jobs, running them inside one document
    /// batch. Returns false when no job was due.
    /// </summary>
    /// <param name="stoppingToken">The host stopping token.</param>
    /// <returns>Whether any job ran.</returns>
    internal async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        JobLease? lease = await _jobRuntime.ClaimNextAsync(JobTypes, WorkerId, LeaseDuration, stoppingToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return false;
        }

        _processingProgress?.NotifyChanged();
        string filePath = ResolvePayloadFilePath(lease.Payload);
        using (PdfDocumentBatch.Begin(filePath))
        {
            await ExecuteJobAsync(lease, stoppingToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(lease.BookId))
            {
                for (int index = 1; index < MaxJobsPerBookBatch && !stoppingToken.IsCancellationRequested; index++)
                {
                    JobLease? sibling = await _jobRuntime
                        .ClaimNextForBookAsync(JobTypes, lease.BookId, WorkerId, LeaseDuration, stoppingToken)
                        .ConfigureAwait(false);
                    if (sibling is null)
                    {
                        break;
                    }

                    await ExecuteJobAsync(sibling, stoppingToken).ConfigureAwait(false);
                }
            }
        }

        return true;
    }

    private async Task ExecuteJobAsync(JobLease lease, CancellationToken stoppingToken)
    {
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task heartbeat = RenewLeaseAsync(lease.JobId, heartbeatCancellation.Token);

        try
        {
            (bool success, JobFailure? failure) = await RunJobAsync(lease, stoppingToken).ConfigureAwait(false);
            if (success)
            {
                await _jobRuntime.CompleteAsync(lease.JobId, WorkerId, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                await FailSafelyAsync(lease, failure!).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown is not an attempt: hand the job back untouched (T06.2).
            await ReleaseSafelyAsync(lease).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            InfrastructureLog.DegradedStep(_logger, exception, nameof(BookIngestionWorker), "jobs.execute");
            await FailSafelyAsync(
                    lease,
                    new JobFailure("worker_exception", "The worker failed while processing the job.", Retryable: true))
                .ConfigureAwait(false);
        }
        finally
        {
            await heartbeatCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await heartbeat.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (heartbeatCancellation.IsCancellationRequested)
            {
                // Intentionally ignored: the heartbeat stops with its job.
            }

            _processingProgress?.NotifyChanged();
        }
    }

    private async Task<(bool Success, JobFailure? Failure)> RunJobAsync(JobLease lease, CancellationToken stoppingToken)
    {
        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(stoppingToken)
            .ConfigureAwait(false);
        string? bookId = lease.BookId;
        var book = await context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.Sha256Hash, b.IsPasswordProtected })
            .FirstOrDefaultAsync(stoppingToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            // The book was removed while its work was queued.
            return (false, JobFailure.Cancelled("book_removed", "The book is no longer in the catalogue."));
        }

        string filePath = ResolvePayloadFilePath(lease.Payload);
        if (lease.JobType is "ThumbnailGeneration" or "SpineGeneration" && book.IsPasswordProtected)
        {
            // A locked PDF keeps its placeholder until it is unlocked (Phase 12); not a failure.
            return (false, JobFailure.Cancelled("pdf_locked", "The PDF is password-protected."));
        }

        bool success;
        string? errorMessage;
        switch (lease.JobType)
        {
            case "MetadataExtraction":
                (success, errorMessage) = await _metadataExtraction.ExtractAsync(
                    bookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);
                if (success && !string.IsNullOrWhiteSpace(bookId))
                {
                    // Persist the follow-up job before the leased source job completes.
                    TryAddEnrichJob(context, bookId, filePath, book.Sha256Hash ?? filePath);
                    await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                }

                break;
            case "Enrich":
                (success, errorMessage) = await _metadataEnrichment.EnrichAsync(
                    bookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);
                break;
            case "ThumbnailGeneration" when !string.IsNullOrEmpty(book.Sha256Hash):
                (success, errorMessage) = await _thumbnailService.GenerateCoverAsync(
                    bookId ?? string.Empty, book.Sha256Hash, filePath, stoppingToken)
                    .ConfigureAwait(false);
                break;
            case "SpineGeneration" when !string.IsNullOrEmpty(book.Sha256Hash):
                (success, errorMessage) = await _spineService.GenerateSpineAsync(
                    bookId ?? string.Empty, book.Sha256Hash, filePath, stoppingToken)
                    .ConfigureAwait(false);
                break;
            default:
                return (false, new JobFailure(
                    "job_incomplete",
                    "The job type is unknown or the book has no content hash.",
                    Retryable: false,
                    DeadLetter: true));
        }

        if (success)
        {
            return (true, null);
        }

        bool missingFile = string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath);
        return (false, missingFile
            ? new JobFailure("file_missing", "The PDF file is not available.", Retryable: false, Kind: JobFailureKind.Permanent)
            : new JobFailure("job_failed", errorMessage, Retryable: true));
    }

    private async Task FailSafelyAsync(JobLease lease, JobFailure failure)
    {
        try
        {
            await _jobRuntime.FailAsync(lease.JobId, WorkerId, failure, cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            // The lease stays with this process; recovery or expiry returns the job (T06.5).
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(BookIngestionWorker), "jobs.fail");
        }
    }

    private async Task ReleaseSafelyAsync(JobLease lease)
    {
        try
        {
            await _jobRuntime.ReleaseAsync(lease.JobId, WorkerId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            // Startup recovery returns the job without consuming an attempt.
            InfrastructureLog.RecoveryStepFailed(_logger, exception, nameof(BookIngestionWorker), "jobs.release");
        }
    }

    private async Task RenewLeaseAsync(long jobId, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(LeaseDuration / 2);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await _jobRuntime.RenewAsync(jobId, WorkerId, LeaseDuration, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Intentionally ignored: the heartbeat ends with its job.
        }
    }

    private static string WorkerId => $"book-ingestion-{Environment.MachineName}-{Environment.ProcessId}";

    private static string ResolvePayloadFilePath(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        if (!payload.TrimStart().StartsWith('{'))
        {
            return payload;
        }

        try
        {
            BatchEnrichmentJobPayload? batchPayload =
                JsonSerializer.Deserialize<BatchEnrichmentJobPayload>(payload);
            return batchPayload?.FilePath ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void TryAddEnrichJob(
        CatalogueDbContext context,
        string bookId,
        string filePath,
        string idempotencyDiscriminator)
    {
        string idempotencyKey = ComputeIdempotencyKey(bookId, "Enrich", idempotencyDiscriminator);
        JobRow? existing = context.Jobs.FirstOrDefault(j => j.IdempotencyKey == idempotencyKey);
        if (existing is null)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = "Enrich",
                IdempotencyKey = idempotencyKey,
                Status = 0,
                BookId = bookId,
                Payload = filePath,
            });
            return;
        }

        if (existing.Status is 3 or 4)
        {
            // New work, not another attempt (Sept-23 Phase 06, T06.2).
            existing.Status = 0;
            existing.Payload = filePath;
            existing.StartedUtc = null;
            existing.CompletedUtc = null;
            existing.ErrorMessage = null;
            existing.RetryCount = 0;
            existing.RequeueCount += 1;
        }
    }

    private static string ComputeIdempotencyKey(
        string bookId,
        string jobType,
        string idempotencyDiscriminator)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}|{idempotencyDiscriminator}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
