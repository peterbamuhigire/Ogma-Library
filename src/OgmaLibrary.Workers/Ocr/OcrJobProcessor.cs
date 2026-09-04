using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Workers.Ocr;

/// <summary>Processes queued OCR jobs from the shared job table.</summary>
internal interface IOcrJobProcessor
{
    /// <summary>Processes one pending or interrupted OCR job, if present.</summary>
    /// <returns><see langword="true"/> when a job was processed.</returns>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}

/// <summary>Processes resumable Phase 15 OCR jobs from the shared Jobs table.</summary>
internal sealed class OcrJobProcessor : IOcrJobProcessor
{
    /// <summary>Jobs table type for OCR work.</summary>
    public const string JobType = "OcrJob";

    private const string OcrSource = "OCR";
    private const string OcrModelVersion = "tesseract-v1";
    private const int MaximumPagesPerJob = 10_000;
    private const int MaximumRenderedImageBytes = 64 * 1024 * 1024;
    private const string WorkerId = "ocr-worker";
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IOcrProvider _ocrProvider;
    private readonly IExtractedTextStore _textStore;
    private readonly ISearchChunkRepository _chunkRepository;
    private readonly SearchChunker _chunker;
    private readonly IJobRuntimeService _jobRuntime;

    /// <summary>Initializes a new instance of <see cref="OcrJobProcessor"/>.</summary>
    public OcrJobProcessor(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IPdfRendererFactory rendererFactory,
        IOcrProvider ocrProvider,
        IExtractedTextStore textStore,
        ISearchChunkRepository chunkRepository,
        SearchChunker chunker,
        IJobRuntimeService? jobRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        ArgumentNullException.ThrowIfNull(ocrProvider);
        ArgumentNullException.ThrowIfNull(textStore);
        ArgumentNullException.ThrowIfNull(chunkRepository);
        ArgumentNullException.ThrowIfNull(chunker);

        _contextFactory = contextFactory;
        _rendererFactory = rendererFactory;
        _ocrProvider = ocrProvider;
        _textStore = textStore;
        _chunkRepository = chunkRepository;
        _chunker = chunker;
        _jobRuntime = jobRuntime ?? new JobRuntimeService(contextFactory);
    }

    /// <inheritdoc />
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        JobLease? lease = await _jobRuntime.ClaimNextAsync(
                [JobType],
                WorkerId,
                TimeSpan.FromMinutes(15),
                cancellationToken)
            .ConfigureAwait(false);
        if (lease is null)
        {
            return false;
        }

        using CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        JobRow? job = await context.Jobs
            .FirstOrDefaultAsync(j => j.JobId == lease.JobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            await _jobRuntime.FailAsync(
                    lease.JobId,
                    WorkerId,
                    new JobFailure("job_missing", "The claimed OCR job no longer exists.", Retryable: false),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        OcrProcessingResult processing = await ProcessAsync(context, job, cancellationToken).ConfigureAwait(false);
        if (processing.Succeeded)
        {
            await _jobRuntime.CompleteAsync(lease.JobId, WorkerId, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _jobRuntime.FailAsync(
                    lease.JobId,
                    WorkerId,
                new JobFailure(
                    processing.FailureCode ?? "ocr_processing_failed",
                    "OCR processing failed; the job was returned to the bounded retry policy.",
                    Retryable: true),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    private async Task<OcrProcessingResult> ProcessAsync(
        CatalogueDbContext context,
        JobRow job,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.BookId))
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OcrProcessingResult.Failed("ocr_invalid_payload");
        }

        OcrJobPayload payload = ParsePayload(job.Payload);
        if (string.IsNullOrWhiteSpace(payload.FilePath))
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OcrProcessingResult.Failed("ocr_invalid_payload");
        }

        job.StartedUtc ??= DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(payload.FilePath);
            int totalPages = renderer.PageCount;
            if (totalPages < 0 || totalPages > MaximumPagesPerJob)
            {
                throw new OcrResourceLimitException("ocr_page_limit");
            }
            string? contentHash = await context.Books
                .Where(book => book.BookId == job.BookId)
                .Select(book => book.Sha256Hash)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
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

                TextLayer nativeLayer = await Task.Run(
                        () => renderer.ExtractTextLayer(pageIndex),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!OcrPageQualityPolicy.ShouldProcess(nativeLayer.Quality, nativeLayer.Words.Count))
                {
                    continue;
                }

                RenderResult rendered = await renderer
                    .RenderPageAsync(pageIndex, new RenderRequest(2400, Scale: 3.125), cancellationToken)
                    .ConfigureAwait(false);
                if (rendered.PngBytes.Length > MaximumRenderedImageBytes)
                {
                    throw new OcrResourceLimitException("ocr_render_limit");
                }

                using var image = new MemoryStream(rendered.PngBytes, writable: false);
                OcrPageResult result = await _ocrProvider
                    .RecognizeAsync(image, payload.Language, cancellationToken)
                    .ConfigureAwait(false);

                await _textStore.UpsertPageAsync(
                    ToExtractedPage(job.BookId, pageIndex, result, payload.Language, contentHash),
                    cancellationToken).ConfigureAwait(false);

                completedPages.Add(pageIndex);
                payload = payload with { ProcessedPages = completedPages.Count };
                await SaveProgressAsync(context, job, payload, cancellationToken).ConfigureAwait(false);
            }

            int chunkCount = await ReplaceOcrSearchChunksAsync(job.BookId, cancellationToken).ConfigureAwait(false);
            await MarkBookOcrDerivedAsync(context, job.BookId, cancellationToken).ConfigureAwait(false);
            TryAddFtsReindexJob(context, job.BookId);
            if (chunkCount > 0)
            {
                TryAddEmbeddingJob(context, job.BookId);
            }

            job.ErrorMessage = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OcrProcessingResult.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OcrResourceLimitException error)
        {
            return OcrProcessingResult.Failed(error.Code);
        }
        catch (Exception)
        {
            return OcrProcessingResult.Failed("ocr_processing_failed");
        }
    }

    private sealed record OcrProcessingResult(bool Succeeded, string? FailureCode)
    {
        public static OcrProcessingResult Success { get; } = new(true, null);

        public static OcrProcessingResult Failed(string code) => new(false, code);
    }

    private sealed class OcrResourceLimitException(string code) : InvalidOperationException
    {
        public string Code { get; } = code;
    }

    private async Task<int> ReplaceOcrSearchChunksAsync(string bookId, CancellationToken cancellationToken)
    {
        using CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ExtractedPageRow> pages = await context.ExtractedPages
            .Where(page => page.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var chunks = new List<SearchChunkRecord>();
        int chunkIndex = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (IGrouping<int, ExtractedPageRow> pageGroup in pages
                     .GroupBy(page => page.PageNumber)
                     .OrderBy(group => group.Key))
        {
            ExtractedPageRow? primary = pageGroup.FirstOrDefault(page => page.Source == "Extraction");
            ExtractedPageRow? ocr = pageGroup.FirstOrDefault(page => page.Source == OcrSource);
            bool selectOcr = ocr is not null && OcrPageQualityPolicy.ShouldSelectOcr(
                primary is null
                    ? SearchExtractionQuality.Empty
                    : (SearchExtractionQuality)primary.ExtractionQuality,
                primary?.WordCount ?? 0,
                ocr.TextContent,
                ocr.OcrConfidence ?? 0);

            if (primary is not null)
            {
                primary.IsSelectedText = !selectOcr;
            }

            if (ocr is not null)
            {
                ocr.IsSelectedText = selectOcr;
            }

            ExtractedPageRow? page = selectOcr ? ocr : primary;
            if (page is null ||
                !page.IsSelectedText ||
                string.IsNullOrWhiteSpace(page.TextContent) ||
                page.ExtractionQuality == (int)SearchExtractionQuality.Failed)
            {
                continue;
            }

            IReadOnlyList<SearchChunkRecord> pageChunks = _chunker.Chunk(
                bookId,
                SearchChunkSource.Page,
                page.TextContent,
                chunkIndex,
                now,
                page.ExtractedPageId,
                page.PageNumber);
            chunks.AddRange(pageChunks);
            chunkIndex += pageChunks.Count;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SearchChunkRecord> saved = await _chunkRepository.ReplaceForBookAsync(
                bookId,
                SearchChunkSource.Page,
                chunks,
                cancellationToken)
            .ConfigureAwait(false);

        BookRow? book = await context.Books.FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is not null)
        {
            book.IndexStatus = 2;
            book.EmbeddingStatus = 0;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved.Count;
    }

    private static ExtractedPageRecord ToExtractedPage(
        string bookId,
        int pageIndex,
        OcrPageResult result,
        string language,
        string? contentHash)
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
            ContentHash: contentHash,
            ExtractedAtUtc: DateTimeOffset.UtcNow,
            Source: OcrSource,
            ExtractorVersion: OcrModelVersion,
            IsSelectedText: false,
            OcrConfidence: Math.Clamp(result.Confidence, 0, 1),
            OcrLanguage: language,
            OcrModelVersion: OcrModelVersion);
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
            book.IsOcrDerived = await context.ExtractedPages.AnyAsync(
                page => page.BookId == bookId && page.Source == OcrSource && page.IsSelectedText,
                cancellationToken).ConfigureAwait(false);
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

    private static void TryAddEmbeddingJob(CatalogueDbContext context, string bookId)
    {
        string key = ComputeIdempotencyKey(bookId, "EmbeddingJob", OcrSource);
        if (context.Jobs.Any(job => job.IdempotencyKey == key))
        {
            return;
        }

        context.Jobs.Add(new JobRow
        {
            JobType = "EmbeddingJob",
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

    private static string ComputeIdempotencyKey(string bookId, string jobType, string discriminator)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}|{discriminator}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
