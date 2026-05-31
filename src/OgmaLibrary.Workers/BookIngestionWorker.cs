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
    private readonly CatalogueDbContext _context;
    private readonly IMetadataExtractionService _metadataExtraction;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISpineService _spineService;
    private readonly IScanProgressService _progress;
    private readonly IBookMetadataEnrichmentService _metadataEnrichment;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIngestionWorker"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="metadataExtraction">The metadata extraction service.</param>
    /// <param name="thumbnailService">The thumbnail generation service.</param>
    /// <param name="spineService">The spine generation service.</param>
    /// <param name="progress">The scan progress service.</param>
    /// <param name="metadataEnrichment">The deterministic online metadata enrichment service.</param>
    public BookIngestionWorker(
        CatalogueDbContext context,
        IMetadataExtractionService metadataExtraction,
        IThumbnailService thumbnailService,
        ISpineService spineService,
        IScanProgressService progress,
        IBookMetadataEnrichmentService metadataEnrichment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metadataExtraction);
        ArgumentNullException.ThrowIfNull(thumbnailService);
        ArgumentNullException.ThrowIfNull(spineService);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(metadataEnrichment);

        _context = context;
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
            List<JobRow> pending = await _context.Jobs
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

                await ExecuteJobAsync(job, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteJobAsync(JobRow job, CancellationToken stoppingToken)
    {
        // Mark as Running.
        job.Status = 1;
        job.StartedUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            string? contentHash = await _context.Books
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
                    TryAddEnrichJob(job.BookId, filePath);
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
                await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Swallow to prevent worker crash on DB save issues.
            }
        }
    }

    private void TryAddEnrichJob(string bookId, string filePath)
    {
        string idempotencyKey = ComputeIdempotencyKey(bookId, "Enrich");
        bool exists = _context.Jobs.Any(j => j.IdempotencyKey == idempotencyKey);
        if (exists)
        {
            return;
        }

        _context.Jobs.Add(new JobRow
        {
            JobType = "Enrich",
            IdempotencyKey = idempotencyKey,
            Status = 0,
            BookId = bookId,
            Payload = filePath,
        });
    }

    private static string ComputeIdempotencyKey(string bookId, string jobType)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
