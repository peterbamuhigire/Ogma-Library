using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Workers.Ocr;

/// <summary>Processes queued OCR jobs from the shared job table.</summary>
public interface IOcrJobProcessor
{
    /// <summary>Processes one pending or interrupted OCR job, if present.</summary>
    /// <returns><see langword="true"/> when a job was processed.</returns>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}

/// <summary>Processes resumable Phase 15 OCR jobs from the shared Jobs table.</summary>
public sealed class OcrJobProcessor : IOcrJobProcessor
{
    /// <summary>Jobs table type for OCR work.</summary>
    public const string JobType = "OcrJob";

    private const string OcrSource = "OCR";
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IOcrProvider _ocrProvider;
    private readonly IExtractedTextStore _textStore;

    /// <summary>Initializes a new instance of <see cref="OcrJobProcessor"/>.</summary>
    public OcrJobProcessor(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IPdfRendererFactory rendererFactory,
        IOcrProvider ocrProvider,
        IExtractedTextStore textStore)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        ArgumentNullException.ThrowIfNull(ocrProvider);
        ArgumentNullException.ThrowIfNull(textStore);

        _contextFactory = contextFactory;
        _rendererFactory = rendererFactory;
        _ocrProvider = ocrProvider;
        _textStore = textStore;
    }

    /// <inheritdoc />
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        JobRow? job = await context.Jobs
            .Where(j => j.JobType == JobType && (j.Status == 0 || j.Status == 1))
            .OrderBy(j => j.JobId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return false;
        }

        await ProcessAsync(context, job, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ProcessAsync(CatalogueDbContext context, JobRow job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.BookId))
        {
            Fail(job, "OCR job has no BookId.");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        OcrJobPayload payload = ParsePayload(job.Payload);
        if (string.IsNullOrWhiteSpace(payload.FilePath))
        {
            Fail(job, "OCR job has no source file path.");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        bool isRecovery = job.Status == 1;
        job.Status = 1;
        job.StartedUtc ??= DateTimeOffset.UtcNow;
        if (isRecovery)
        {
            job.RetryCount += 1;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(payload.FilePath);
            int totalPages = renderer.PageCount;
            HashSet<int> completedPages = await LoadCompletedOcrPagesAsync(context, job.BookId, cancellationToken)
                .ConfigureAwait(false);

            payload = payload with
            {
                TotalPages = totalPages,
                ProcessedPages = completedPages.Count,
            };
            await SaveProgressAsync(context, job, payload, cancellationToken).ConfigureAwait(false);

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (completedPages.Contains(pageIndex))
                {
                    continue;
                }

                RenderResult rendered = await renderer
                    .RenderPageAsync(pageIndex, new RenderRequest(2400, Scale: 3.125), cancellationToken)
                    .ConfigureAwait(false);

                using var image = new MemoryStream(rendered.PngBytes, writable: false);
                OcrPageResult result = await _ocrProvider
                    .RecognizeAsync(image, payload.Language, cancellationToken)
                    .ConfigureAwait(false);

                await _textStore.UpsertPageAsync(
                    ToExtractedPage(job.BookId, pageIndex, result),
                    cancellationToken).ConfigureAwait(false);

                completedPages.Add(pageIndex);
                payload = payload with { ProcessedPages = completedPages.Count };
                await SaveProgressAsync(context, job, payload, cancellationToken).ConfigureAwait(false);
            }

            await MarkBookOcrDerivedAsync(context, job.BookId, cancellationToken).ConfigureAwait(false);
            TryAddFtsReindexJob(context, job.BookId);
            job.Status = 2;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.ErrorMessage = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            job.Status = 0;
            job.StartedUtc = null;
            job.RetryCount += 1;
            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            Fail(job, ex.Message);
            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static ExtractedPageRecord ToExtractedPage(
        string bookId,
        int pageIndex,
        OcrPageResult result)
    {
        string text = result.Text ?? string.Empty;
        int wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return new ExtractedPageRecord(
            Id: 0,
            BookId: bookId,
            PageIndex: pageIndex,
            Text: text,
            Quality: string.IsNullOrWhiteSpace(text) ? SearchExtractionQuality.Empty : SearchExtractionQuality.Full,
            WordCount: wordCount,
            ContentHash: null,
            ExtractedAtUtc: DateTimeOffset.UtcNow,
            Source: OcrSource);
    }

    private static async Task<HashSet<int>> LoadCompletedOcrPagesAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        List<int> pages = await context.ExtractedPages
            .AsNoTracking()
            .Where(page => page.BookId == bookId && page.Source == OcrSource)
            .Select(page => page.PageNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return pages.ToHashSet();
    }

    private static async Task MarkBookOcrDerivedAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        BookRow? book = await context.Books
            .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is not null)
        {
            book.IsOcrDerived = true;
        }
    }

    private static void TryAddFtsReindexJob(CatalogueDbContext context, string bookId)
    {
        string key = ComputeIdempotencyKey(bookId, "FtsReindexJob", OcrSource);
        if (context.Jobs.Any(job => job.IdempotencyKey == key))
        {
            return;
        }

        context.Jobs.Add(new JobRow
        {
            JobType = "FtsReindexJob",
            BookId = bookId,
            IdempotencyKey = key,
            Status = 0,
            Payload = $"{{\"bookId\":\"{bookId}\",\"source\":\"OCR\"}}",
        });
    }

    private static async Task SaveProgressAsync(
        CatalogueDbContext context,
        JobRow job,
        OcrJobPayload payload,
        CancellationToken cancellationToken)
    {
        job.Payload = JsonSerializer.Serialize(payload);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OcrJobPayload ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new OcrJobPayload(string.Empty);
        }

        return JsonSerializer.Deserialize<OcrJobPayload>(payload) ?? new OcrJobPayload(string.Empty);
    }

    private static void Fail(JobRow job, string error)
    {
        job.Status = 3;
        job.ErrorMessage = error;
        job.CompletedUtc = DateTimeOffset.UtcNow;
    }

    private static string ComputeIdempotencyKey(string bookId, string jobType, string discriminator)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}|{discriminator}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
