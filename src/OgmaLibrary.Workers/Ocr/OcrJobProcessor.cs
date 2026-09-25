using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Ocr;

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
    private readonly BookTextStatusService _textStatus;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of <see cref="OcrJobProcessor"/>.</summary>
    public OcrJobProcessor(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IPdfRendererFactory rendererFactory,
        IOcrProvider ocrProvider,
        IExtractedTextStore textStore,
        ISearchChunkRepository chunkRepository,
        SearchChunker chunker,
        IJobRuntimeService? jobRuntime = null,
        ILogger<OcrJobProcessor>? logger = null)
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
        _textStatus = new BookTextStatusService(contextFactory);
        _logger = logger ?? (ILogger)NullLogger.Instance;
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

        // Sept-23 Phase 17: the badge says "OCR in progress" while this job runs (a retry from
        // the Activity Centre does not go through the queue service).
        await RefreshTextStatusAsync(job.BookId, cancellationToken).ConfigureAwait(false);
        OcrProcessingResult processing = await ProcessAsync(context, job, cancellationToken).ConfigureAwait(false);
        if (processing.Stopped ||
            await IsStopRequestedAsync(lease.JobId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        try
        {
            if (processing.Succeeded)
            {
                await _jobRuntime.CompleteAsync(lease.JobId, WorkerId, cancellationToken)
                    .ConfigureAwait(false);
                BookTextAssessment? assessment = await RefreshTextStatusAsync(job.BookId, cancellationToken)
                    .ConfigureAwait(false);
                InfrastructureLog.OcrJobCompleted(
                    _logger,
                    lease.JobId,
                    processing.PagesRecognised,
                    processing.Chunks,
                    assessment?.Status ?? BookTextStatus.Unknown);
            }
            else
            {
                // Sept-23 Phase 17 (task 4): a typed, logged failure on the job; codes that a
                // retry cannot fix (missing language data, resource limits) are permanent.
                string code = processing.FailureCode ?? OcrFailureCodes.Unexpected;
                bool retryable = OcrFailureCodes.IsRetryable(code);
                InfrastructureLog.OcrJobFailed(_logger, processing.Error, lease.JobId, code, retryable);
                await _jobRuntime.FailAsync(
                        lease.JobId,
                        WorkerId,
                        new JobFailure(
                            code,
                            retryable
                                ? "OCR processing failed; the job was returned to the bounded retry policy."
                                : "OCR processing failed and cannot succeed by retrying.",
                            Retryable: retryable),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await RefreshTextStatusAsync(job.BookId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
            if (!await IsStopRequestedAsync(lease.JobId, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }
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
            return OcrProcessingResult.Failed(OcrFailureCodes.InvalidJob);
        }

        OcrJobPayload payload;
        try
        {
            payload = ParsePayload(job.Payload);
        }
        catch (JsonException error)
        {
            return OcrProcessingResult.Failed(OcrFailureCodes.InvalidJob, error);
        }

        if (string.IsNullOrWhiteSpace(payload.FilePath))
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OcrProcessingResult.Failed(OcrFailureCodes.InvalidJob);
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
            int recognisedThisRun = 0;

            payload = payload with
            {
                TotalPages = totalPages,
                ProcessedPages = completedPages.Count,
            };
            await SaveProgressAsync(context, job, payload, cancellationToken).ConfigureAwait(false);

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsStopRequestedAsync(job.JobId, cancellationToken).ConfigureAwait(false))
                {
                    return OcrProcessingResult.StoppedByControl;
                }

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
                recognisedThisRun++;
                payload = payload with { ProcessedPages = completedPages.Count };
                await SaveProgressAsync(context, job, payload, cancellationToken).ConfigureAwait(false);
            }

            if (await IsStopRequestedAsync(job.JobId, cancellationToken).ConfigureAwait(false))
            {
                return OcrProcessingResult.StoppedByControl;
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
            return OcrProcessingResult.Completed(recognisedThisRun, chunkCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(error))
        {
            // Sept-23 Phase 17 (task 4): never swallowed; classified, logged by the caller and
            // recorded on the job with a code the UI maps to a localised reason.
            return OcrProcessingResult.Failed(Classify(error), error);
        }
    }

    /// <summary>Maps an OCR-time exception to a stable failure code (Sept-23 Phase 17).</summary>
    /// <param name="error">The exception.</param>
    /// <returns>A code from <see cref="OcrFailureCodes"/>.</returns>
    internal static string Classify(Exception error) => error switch
    {
        OcrFailureException typed => typed.Code,
        OcrResourceLimitException => OcrFailureCodes.ResourceLimit,
        TimeoutException or OperationCanceledException => OcrFailureCodes.Timeout,
        FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException => OcrFailureCodes.FileUnavailable,
        PdfPasswordRequiredException or PdfPasswordIncorrectException => OcrFailureCodes.UnreadablePage,
        PdfRendererUnavailableException or IOException => OcrFailureCodes.UnreadablePage,
        _ => OcrFailureCodes.Unexpected,
    };

    private async Task<BookTextAssessment?> RefreshTextStatusAsync(string? bookId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookId))
        {
            return null;
        }

        try
        {
            return await _textStatus.RefreshAsync(bookId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            InfrastructureLog.DegradedStep(_logger, error, nameof(OcrJobProcessor), "ocr.text_status");
            return null;
        }
    }

    private sealed record OcrProcessingResult(
        bool Succeeded,
        bool Stopped,
        string? FailureCode,
        Exception? Error = null,
        int PagesRecognised = 0,
        int Chunks = 0)
    {
        public static OcrProcessingResult StoppedByControl { get; } = new(false, true, null);

        public static OcrProcessingResult Completed(int pages, int chunks) => new(true, false, null, null, pages, chunks);

        public static OcrProcessingResult Failed(string code, Exception? error = null) => new(false, false, code, error);
    }

    private sealed class OcrResourceLimitException : InvalidOperationException
    {
        public OcrResourceLimitException()
        {
        }

        public OcrResourceLimitException(string message)
            : base(message)
        {
        }

        public OcrResourceLimitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private async Task<bool> IsStopRequestedAsync(long jobId, CancellationToken cancellationToken)
    {
        using CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        int? status = await context.Jobs
            .AsNoTracking()
            .Where(job => job.JobId == jobId)
            .Select(job => (int?)job.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return status is (int)JobRuntimeStatus.Cancelled or (int)JobRuntimeStatus.Paused;
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
