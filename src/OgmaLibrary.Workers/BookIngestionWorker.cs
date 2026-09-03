using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Workers;

/// <summary>
/// Background worker that processes pending background jobs (MetadataExtraction,
/// ThumbnailGeneration, SpineGeneration) from the Jobs queue (NFR-OGMA-009,
/// NFR-PROD-005). Per-file failure isolation: one failing job never cancels siblings.
/// All heavy work runs off the UI thread.
/// </summary>
public sealed class BookIngestionWorker : BackgroundService
{
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IJobRuntimeService _jobRuntime;
    private readonly IMetadataExtractionService _metadataExtraction;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISpineService _spineService;
    private readonly IScanProgressService _progress;
    private readonly IBookMetadataEnrichmentService _metadataEnrichment;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIngestionWorker"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="jobRuntime">The durable lease runtime.</param>
    /// <param name="metadataExtraction">The metadata extraction service.</param>
    /// <param name="thumbnailService">The thumbnail generation service.</param>
    /// <param name="spineService">The spine generation service.</param>
    /// <param name="progress">The scan progress service.</param>
    /// <param name="metadataEnrichment">The deterministic online metadata enrichment service.</param>
    public BookIngestionWorker(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IJobRuntimeService jobRuntime,
        IMetadataExtractionService metadataExtraction,
        IThumbnailService thumbnailService,
        ISpineService spineService,
        IScanProgressService progress,
        IBookMetadataEnrichmentService metadataEnrichment)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(jobRuntime);
        ArgumentNullException.ThrowIfNull(metadataExtraction);
        ArgumentNullException.ThrowIfNull(thumbnailService);
        ArgumentNullException.ThrowIfNull(spineService);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(metadataEnrichment);

        _contextFactory = contextFactory;
        _jobRuntime = jobRuntime;
        _metadataExtraction = metadataExtraction;
        _thumbnailService = thumbnailService;
        _spineService = spineService;
        _progress = progress;
        _metadataEnrichment = metadataEnrichment;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Poll the Jobs table for pending work.
        while (!stoppingToken.IsCancellationRequested)
        {
            JobLease? lease = await _jobRuntime.ClaimNextAsync(
                ["MetadataExtraction", "Enrich", "ThumbnailGeneration", "SpineGeneration"],
                WorkerId,
                LeaseDuration,
                stoppingToken).ConfigureAwait(false);

            if (lease is null)
            {
                if (_progress.CurrentSnapshot.Phase == ScanPhase.GeneratingAssets)
                {
                    _progress.SetPhase(ScanPhase.Complete);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            _progress.SetPhase(ScanPhase.GeneratingAssets);

            await ExecuteJobAsync(lease, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteJobAsync(JobLease lease, CancellationToken stoppingToken)
    {
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task heartbeat = RenewLeaseAsync(lease.JobId, heartbeatCancellation.Token);

        try
        {
            using CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(stoppingToken)
                .ConfigureAwait(false);
            string? bookId = lease.BookId;
            string? contentHash = await context.Books
                .AsNoTracking()
                .Where(b => b.BookId == bookId)
                .Select(b => b.Sha256Hash)
                .FirstOrDefaultAsync(stoppingToken)
                .ConfigureAwait(false);

            string filePath = ResolvePayloadFilePath(lease.Payload);
            bool success;
            string? errorMessage;

            if (lease.JobType == "MetadataExtraction")
            {
                (success, errorMessage) = await _metadataExtraction.ExtractAsync(
                    bookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);

                if (success && !string.IsNullOrWhiteSpace(bookId))
                {
                    TryAddEnrichJob(context, bookId, filePath, contentHash ?? filePath);
                }
            }
            else if (lease.JobType == "Enrich")
            {
                (success, errorMessage) = await _metadataEnrichment.EnrichAsync(
                    bookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);
            }
            else if (lease.JobType == "ThumbnailGeneration" && !string.IsNullOrEmpty(contentHash))
            {
                (success, errorMessage) = await _thumbnailService.GenerateCoverAsync(
                    bookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (lease.JobType == "SpineGeneration" && !string.IsNullOrEmpty(contentHash))
            {
                (success, errorMessage) = await _spineService.GenerateSpineAsync(
                    bookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else
            {
                success = false;
                errorMessage = $"Unknown or incomplete job: type={lease.JobType}, contentHash={(contentHash is null ? "null" : "present")}";
            }

            // Persist any follow-up jobs (for example Enrich after successful
            // metadata extraction) before the leased source job is completed.
            await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

            if (success)
            {
                await _jobRuntime.CompleteAsync(lease.JobId, WorkerId, stoppingToken).ConfigureAwait(false);
                _progress.IncrementCompleted();
            }
            else
            {
                await _jobRuntime.FailAsync(
                    lease.JobId,
                    WorkerId,
                    new JobFailure(
                        "job_failed",
                        errorMessage,
                        Retryable: true,
                        DeadLetter: lease.JobType is not (
                            "MetadataExtraction" or "Enrich" or "ThumbnailGeneration" or "SpineGeneration")),
                    cancellationToken: stoppingToken).ConfigureAwait(false);
                _progress.IncrementFailed();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _jobRuntime.FailAsync(
                lease.JobId,
                WorkerId,
                new JobFailure("worker_exception", ex.Message, Retryable: true),
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            _progress.IncrementFailed();
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
            }
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
        }
    }

    private static string WorkerId => $"book-ingestion-{Environment.MachineName}-{Environment.ProcessId}";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

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
            existing.Status = 0;
            existing.Payload = filePath;
            existing.StartedUtc = null;
            existing.CompletedUtc = null;
            existing.ErrorMessage = null;
            existing.RetryCount += 1;
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
