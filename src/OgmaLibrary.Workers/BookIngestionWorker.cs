using System.Security.Cryptography;
using System.Text;
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
    private readonly IMetadataExtractionService _metadataExtraction;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISpineService _spineService;
    private readonly IScanProgressService _progress;
    private readonly IBookMetadataEnrichmentService _metadataEnrichment;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIngestionWorker"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="metadataExtraction">The metadata extraction service.</param>
    /// <param name="thumbnailService">The thumbnail generation service.</param>
    /// <param name="spineService">The spine generation service.</param>
    /// <param name="progress">The scan progress service.</param>
    /// <param name="metadataEnrichment">The deterministic online metadata enrichment service.</param>
    public BookIngestionWorker(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataExtractionService metadataExtraction,
        IThumbnailService thumbnailService,
        ISpineService spineService,
        IScanProgressService progress,
        IBookMetadataEnrichmentService metadataEnrichment)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(metadataExtraction);
        ArgumentNullException.ThrowIfNull(thumbnailService);
        ArgumentNullException.ThrowIfNull(spineService);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(metadataEnrichment);

        _contextFactory = contextFactory;
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
            using CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(stoppingToken)
                .ConfigureAwait(false);

            List<JobRow> pending = await context.Jobs
                .Where(j => j.Status == 0 && // Pending
                    (j.JobType == "MetadataExtraction" ||
                     j.JobType == "Enrich" ||
                     j.JobType == "ThumbnailGeneration" ||
                     j.JobType == "SpineGeneration"))
                .OrderBy(j => j.JobId)
                .Take(10)
                .ToListAsync(stoppingToken)
                .ConfigureAwait(false);

            if (pending.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            _progress.SetPhase(ScanPhase.GeneratingAssets);

            foreach (JobRow job in pending)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ExecuteJobAsync(context, job, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteJobAsync(
        CatalogueDbContext context,
        JobRow job,
        CancellationToken stoppingToken)
    {
        // Mark as Running.
        job.Status = 1;
        job.StartedUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            string? contentHash = await context.Books
                .AsNoTracking()
                .Where(b => b.BookId == job.BookId)
                .Select(b => b.Sha256Hash)
                .FirstOrDefaultAsync(stoppingToken)
                .ConfigureAwait(false);

            string filePath = job.Payload ?? string.Empty;
            bool success;
            string? errorMessage;

            if (job.JobType == "MetadataExtraction")
            {
                (success, errorMessage) = await _metadataExtraction.ExtractAsync(
                    job.BookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);

                if (success && !string.IsNullOrWhiteSpace(job.BookId))
                {
                    TryAddEnrichJob(context, job.BookId, filePath, contentHash ?? filePath);
                }
            }
            else if (job.JobType == "Enrich")
            {
                (success, errorMessage) = await _metadataEnrichment.EnrichAsync(
                    job.BookId ?? string.Empty, filePath, stoppingToken).ConfigureAwait(false);
            }
            else if (job.JobType == "ThumbnailGeneration" && !string.IsNullOrEmpty(contentHash))
            {
                (success, errorMessage) = await _thumbnailService.GenerateCoverAsync(
                    job.BookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (job.JobType == "SpineGeneration" && !string.IsNullOrEmpty(contentHash))
            {
                (success, errorMessage) = await _spineService.GenerateSpineAsync(
                    job.BookId ?? string.Empty, contentHash, filePath, stoppingToken)
                    .ConfigureAwait(false);
            }
            else
            {
                success = false;
                errorMessage = $"Unknown or incomplete job: type={job.JobType}, contentHash={(contentHash is null ? "null" : "present")}";
            }

            job.Status = success ? 2 : 3; // Completed or Failed
            job.ErrorMessage = errorMessage;
            job.CompletedUtc = DateTimeOffset.UtcNow;

            if (success)
            {
                _progress.IncrementCompleted();
            }
            else
            {
                _progress.IncrementFailed();
            }
        }
        catch (OperationCanceledException)
        {
            // Reset to Pending so recovery picks it up on next restart.
            job.Status = 0;
            job.StartedUtc = null;
            throw;
        }
        catch (Exception ex)
        {
            // Per-file isolation: failure recorded, worker continues (NFR-PROD-006).
            job.Status = 3; // Failed
            job.ErrorMessage = ex.Message;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            _progress.IncrementFailed();
        }
        finally
        {
            try
            {
                await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Swallow to prevent worker crash on DB save issues.
            }
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
