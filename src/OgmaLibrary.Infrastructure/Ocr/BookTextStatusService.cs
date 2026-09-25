using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>
/// Derives and persists each book's honest text status, text-quality score and OCR
/// confidence from its extracted pages and latest OCR job (Sept-23 Phase 17, tasks 1, 2, 7).
/// Called when extraction finishes, when OCR is queued, and when an OCR job ends.
/// </summary>
public sealed class BookTextStatusService
{
    /// <summary>Jobs-table type of OCR work.</summary>
    public const string OcrJobType = "OcrJob";

    private const string NativeSource = "Extraction";
    private const string OcrSource = "OCR";
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Initializes the service for runtime composition.</summary>
    /// <param name="contextFactory">The catalogue context factory.</param>
    [ActivatorUtilitiesConstructor]
    public BookTextStatusService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>Initializes the service over one shared context (tests and shared-context services).</summary>
    /// <param name="context">The catalogue context.</param>
    internal BookTextStatusService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Re-derives and stores the text status of one book.</summary>
    /// <param name="bookId">The book identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessment, or <see langword="null"/> when the book does not exist.</returns>
    public async Task<BookTextAssessment?> RefreshAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        CatalogueDbContext context = _context ?? await _contextFactory!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RefreshAsync(context, bookId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_context is null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Marks a book whose document could not be read at all as having no text.</summary>
    /// <param name="bookId">The book identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when stored.</returns>
    public async Task MarkUnreadableAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        CatalogueDbContext context = _context ?? await _contextFactory!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await context.Books
                .Where(book => book.BookId == bookId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(book => book.TextStatus, (int)BookTextStatus.NoText)
                        .SetProperty(book => book.TextQuality, 0.0),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (_context is null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Assesses books whose status is still unknown although extraction has finished (catalogues
    /// created before Phase 17, or books indexed while the app was older). Bounded per call.
    /// </summary>
    /// <param name="maxBooks">Upper bound on books refreshed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of books refreshed.</returns>
    public async Task<int> RefreshPendingAsync(int maxBooks, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBooks);
        CatalogueDbContext context = _context ?? await _contextFactory!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<string> pending = await context.Books
                .AsNoTracking()
                .Where(book => book.TextStatus == (int)BookTextStatus.Unknown &&
                    (book.IndexStatus == (int)SearchBookIndexStatus.Indexed ||
                     book.IndexStatus == (int)SearchBookIndexStatus.Failed) &&
                    !book.IsPasswordProtected &&
                    context.ExtractedPages.Any(page => page.BookId == book.BookId))
                .OrderBy(book => book.BookId)
                .Select(book => book.BookId)
                .Take(maxBooks)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (string bookId in pending)
            {
                await RefreshAsync(context, bookId, cancellationToken).ConfigureAwait(false);
            }

            return pending.Count;
        }
        finally
        {
            if (_context is null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Assesses a book without persisting the result.</summary>
    /// <param name="context">The catalogue context.</param>
    /// <param name="bookId">The book identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessment.</returns>
    internal static async Task<BookTextAssessment> AssessAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        var pages = await context.ExtractedPages
            .AsNoTracking()
            .Where(page => page.BookId == bookId)
            .Select(page => new
            {
                page.PageNumber,
                page.Source,
                page.ExtractionQuality,
                page.WordCount,
                page.IsSelectedText,
                page.OcrConfidence,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var evidence = new List<BookPageTextEvidence>();
        foreach (var group in pages.GroupBy(page => page.PageNumber).OrderBy(group => group.Key))
        {
            var native = group.FirstOrDefault(page => page.Source == NativeSource);
            if (native is null)
            {
                continue;
            }

            var ocr = group.FirstOrDefault(page => page.Source == OcrSource);
            evidence.Add(new BookPageTextEvidence(
                group.Key,
                (SearchExtractionQuality)native.ExtractionQuality,
                native.WordCount,
                HasOcrRow: ocr is not null,
                OcrSelected: ocr is { IsSelectedText: true, WordCount: > 0 },
                OcrConfidence: ocr?.OcrConfidence));
        }

        (BookOcrJobState jobState, _) = await LoadOcrJobStateAsync(context, bookId, cancellationToken).ConfigureAwait(false);
        return BookTextStatusPolicy.Assess(evidence, jobState);
    }

    /// <summary>Returns the state and OCR failure code of the latest OCR job for a book.</summary>
    /// <param name="context">The catalogue context.</param>
    /// <param name="bookId">The book identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job state and its failure code, if any.</returns>
    internal static async Task<(BookOcrJobState State, string? FailureCode)> LoadOcrJobStateAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        var job = await context.Jobs
            .AsNoTracking()
            .Where(row => row.JobType == OcrJobType && row.BookId == bookId)
            .OrderByDescending(row => row.JobId)
            .Select(row => new { row.Status, row.FailureCode })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return (BookOcrJobState.None, null);
        }

        BookOcrJobState state = (JobRuntimeStatus)job.Status switch
        {
            JobRuntimeStatus.Completed => BookOcrJobState.Completed,
            JobRuntimeStatus.Failed or JobRuntimeStatus.DeadLetter => BookOcrJobState.Failed,
            JobRuntimeStatus.Cancelled => BookOcrJobState.Cancelled,
            _ => BookOcrJobState.Active,
        };
        string? code = state != BookOcrJobState.Completed && OcrFailureCodes.IsOcrCode(job.FailureCode)
            ? job.FailureCode
            : null;
        return (state, code);
    }

    private static async Task<BookTextAssessment?> RefreshAsync(
        CatalogueDbContext context,
        string bookId,
        CancellationToken cancellationToken)
    {
        BookTextAssessment assessment = await AssessAsync(context, bookId, cancellationToken).ConfigureAwait(false);
        double? confidence = assessment.OcrConfidence;
        int updated = await context.Books
            .Where(book => book.BookId == bookId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(book => book.TextStatus, (int)assessment.Status)
                    .SetProperty(book => book.TextQuality, assessment.TextQuality)
                    .SetProperty(book => book.OcrConfidence, confidence),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 0 ? null : assessment;
    }
}
