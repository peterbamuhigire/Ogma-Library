using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Phase 10 extraction pipeline. It opens each PDF through the Reader adapter
/// boundary, persists per-page text quality, and replaces source-scoped chunks
/// so reruns and rebuilds cannot accumulate duplicates.
/// </summary>
public sealed class ExtractionPipelineService : IExtractionPipelineService, IStagedExtractionPipelineService
{
    private const int ActiveBookStatus = 0;
    private const int JobFailed = 3;
    private const string ExtractorVersion = "pdf-text-v1";
    private const string IndexVersion = "fts5-v1";

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IBookFileLocator _fileLocator;
    private readonly IPdfRendererFactory _rendererFactory;
    private readonly IExtractedTextStore _extractedTextStore;
    private readonly ISearchChunkRepository _chunkRepository;
    private readonly SearchChunker _chunker;
    private readonly IExtractionArtifactService _artifactService;
    private readonly IIsbnDetectionService _isbnDetection;
    private readonly IIsbnEvidenceStore _isbnEvidenceStore;
    private readonly ITocExtractionService _tocExtraction;

    /// <summary>
    /// Initializes a new instance of <see cref="ExtractionPipelineService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ExtractionPipelineService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IBookFileLocator fileLocator,
        IPdfRendererFactory rendererFactory,
        IExtractedTextStore extractedTextStore,
        ISearchChunkRepository chunkRepository,
        SearchChunker chunker,
        IExtractionArtifactService artifactService,
        IIsbnDetectionService isbnDetection,
        IIsbnEvidenceStore isbnEvidenceStore,
        ITocExtractionService tocExtraction)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(fileLocator);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        ArgumentNullException.ThrowIfNull(extractedTextStore);
        ArgumentNullException.ThrowIfNull(chunkRepository);
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(artifactService);
        ArgumentNullException.ThrowIfNull(isbnDetection);
        ArgumentNullException.ThrowIfNull(isbnEvidenceStore);
        ArgumentNullException.ThrowIfNull(tocExtraction);

        _contextFactory = contextFactory;
        _fileLocator = fileLocator;
        _rendererFactory = rendererFactory;
        _extractedTextStore = extractedTextStore;
        _chunkRepository = chunkRepository;
        _chunker = chunker;
        _artifactService = artifactService;
        _isbnDetection = isbnDetection;
        _isbnEvidenceStore = isbnEvidenceStore;
        _tocExtraction = tocExtraction;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ExtractionPipelineService"/> for
    /// integration tests that share one context.
    /// </summary>
    internal ExtractionPipelineService(
        CatalogueDbContext context,
        IBookFileLocator fileLocator,
        IPdfRendererFactory rendererFactory,
        IExtractedTextStore extractedTextStore,
        ISearchChunkRepository chunkRepository,
        SearchChunker chunker,
        IIsbnDetectionService? isbnDetection = null,
        IIsbnEvidenceStore? isbnEvidenceStore = null,
        ITocExtractionService? tocExtraction = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fileLocator);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        ArgumentNullException.ThrowIfNull(extractedTextStore);
        ArgumentNullException.ThrowIfNull(chunkRepository);
        ArgumentNullException.ThrowIfNull(chunker);

        _context = context;
        _fileLocator = fileLocator;
        _rendererFactory = rendererFactory;
        _extractedTextStore = extractedTextStore;
        _chunkRepository = chunkRepository;
        _chunker = chunker;
        _artifactService = new ExtractionArtifactService(context);
        _isbnDetection = isbnDetection ?? new IsbnDetectionService();
        _isbnEvidenceStore = isbnEvidenceStore ?? new IsbnEvidenceStore(context);
        _tocExtraction = tocExtraction ?? new PdfTableOfContentsService();
    }

    /// <inheritdoc />
    public async Task<ExtractionBatchResult> IndexNextBatchAsync(
        int maxBooks,
        CancellationToken cancellationToken)
        => await IndexNextBatchAsync(maxBooks, IndexVersion, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ExtractionBatchResult> IndexNextBatchAsync(
        int maxBooks,
        string indexVersion,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBooks);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexVersion);

        IReadOnlyList<string> bookIds = await FindPendingBookIdsAsync(maxBooks, cancellationToken)
            .ConfigureAwait(false);

        int indexed = 0;
        int failed = 0;
        int pagesProcessed = 0;
        int pagesSkipped = 0;
        int failedPages = 0;
        int chunksWritten = 0;

        foreach (string bookId in bookIds)
        {
            ExtractionBookResult result = await IndexBookAsync(bookId, indexVersion, cancellationToken)
                .ConfigureAwait(false);

            if (result.Succeeded)
            {
                indexed++;
            }
            else
            {
                failed++;
            }

            pagesProcessed += result.PagesProcessed;
            pagesSkipped += result.PagesSkipped;
            failedPages += result.FailedPages;
            chunksWritten += result.ChunksWritten;
            // Integration tests may intentionally share one context; clear
            // completed-book tracking so a large batch cannot retain every
            // page/chunk graph. Factory-backed production contexts are already
            // short-lived per operation.
            _context?.ChangeTracker.Clear();
        }

        return new ExtractionBatchResult(
            BooksAttempted: bookIds.Count,
            BooksIndexed: indexed,
            BooksFailed: failed,
            PagesProcessed: pagesProcessed,
            PagesSkipped: pagesSkipped,
            FailedPages: failedPages,
            ChunksWritten: chunksWritten);
    }

    /// <inheritdoc />
    public async Task<ExtractionBookResult> IndexBookAsync(
        string bookId,
        CancellationToken cancellationToken)
        => await IndexBookAsync(bookId, IndexVersion, cancellationToken).ConfigureAwait(false);

    private async Task<ExtractionBookResult> IndexBookAsync(
        string bookId,
        string indexVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        BookIndexSnapshot? book = await LoadBookSnapshotAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return new ExtractionBookResult(bookId, false, 0, 0, 0, 0, "Book was not found.");
        }

        await SetBookStatusAsync(bookId, SearchBookIndexStatus.Extracting, cancellationToken)
            .ConfigureAwait(false);

        ExtractionArtifactDescriptor? artifact = null;
        try
        {
            artifact = await _artifactService.BeginAsync(
                    bookId,
                    book.ContentHash,
                    ExtractorVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordBookFailureAsync(bookId, book.ContentHash, ex.Message, cancellationToken)
                .ConfigureAwait(false);
            return new ExtractionBookResult(bookId, false, 0, 0, 0, 0, ex.Message);
        }

        string? filePath = await _fileLocator.LocateAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            const string message = "No available PDF file was found for indexing.";
            await _artifactService.FailAsync(artifact.Id, CancellationToken.None).ConfigureAwait(false);
            await RecordBookFailureAsync(bookId, book.ContentHash, message, cancellationToken)
                .ConfigureAwait(false);
            return new ExtractionBookResult(bookId, false, 0, 0, 0, 0, message);
        }

        ExtractPagesResult extracted;
        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(filePath);
            extracted = await ExtractPagesAsync(
                    bookId,
                    book.ContentHash,
                    renderer,
                    ExtractorVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (artifact is not null)
            {
                await _artifactService.FailAsync(artifact.Id, CancellationToken.None).ConfigureAwait(false);
            }
            await SetBookStatusAsync(bookId, SearchBookIndexStatus.NotIndexed, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (artifact is not null)
            {
                await _artifactService.FailAsync(artifact.Id, CancellationToken.None).ConfigureAwait(false);
            }
            await RecordBookFailureAsync(bookId, book.ContentHash, ex.Message, cancellationToken)
                .ConfigureAwait(false);
            return new ExtractionBookResult(bookId, false, 0, 0, 0, 0, ex.Message);
        }

        IsbnDetectionResult isbnEvidence = await _isbnDetection
            .DetectAsync(filePath, cancellationToken)
            .ConfigureAwait(false);
        await _isbnEvidenceStore
            .ReplaceAsync(bookId, artifact.Id, isbnEvidence.AllCandidates, cancellationToken)
            .ConfigureAwait(false);

        TocExtractionResult toc = await _tocExtraction
            .ExtractAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<SearchChunkRecord> pageChunks = BuildPageChunks(bookId, extracted.Pages);
        IReadOnlyList<SearchChunkRecord> tocChunks = BuildTocChunks(bookId, toc.Entries);
        IReadOnlyList<SearchChunkRecord> noteChunks = await BuildNoteChunksAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SearchChunkRecord> tagChunks = await BuildTagChunksAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SearchChunkRecord> descriptionChunks = await BuildDescriptionChunksAsync(bookId, cancellationToken)
            .ConfigureAwait(false);

        pageChunks = StampChunks(pageChunks, artifact.Id, indexVersion);
        noteChunks = StampChunks(noteChunks, artifact.Id, indexVersion);
        tagChunks = StampChunks(tagChunks, artifact.Id, indexVersion);
        descriptionChunks = StampChunks(descriptionChunks, artifact.Id, indexVersion);
        tocChunks = StampChunks(tocChunks, artifact.Id, indexVersion);

        int chunksWritten = 0;
        chunksWritten += (await _chunkRepository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Page,
            pageChunks,
            cancellationToken,
            indexVersion).ConfigureAwait(false)).Count;
        chunksWritten += (await _chunkRepository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Note,
            noteChunks,
            cancellationToken,
            indexVersion).ConfigureAwait(false)).Count;
        chunksWritten += (await _chunkRepository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Tag,
            tagChunks,
            cancellationToken,
            indexVersion).ConfigureAwait(false)).Count;
        chunksWritten += (await _chunkRepository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Description,
            descriptionChunks,
            cancellationToken,
            indexVersion).ConfigureAwait(false)).Count;
        chunksWritten += (await _chunkRepository.ReplaceForBookAsync(
                bookId,
                SearchChunkSource.Toc,
                tocChunks,
                cancellationToken,
                indexVersion)
            .ConfigureAwait(false)).Count;

        SearchBookIndexStatus finalStatus = extracted.FailedPages > 0
            ? SearchBookIndexStatus.Failed
            : SearchBookIndexStatus.Indexed;
        string manifestHash = ComputeManifestHash(extracted.Pages, pageChunks, noteChunks, tagChunks, descriptionChunks, tocChunks);
        if (finalStatus == SearchBookIndexStatus.Indexed)
        {
            await _artifactService.CompleteAsync(
                    artifact.Id,
                    extracted.PagesProcessed,
                    extracted.FailedPages,
                    manifestHash,
                    tocEntries: toc.Entries.Count,
                    tocQuality: toc.Quality,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _artifactService.FailAsync(artifact.Id, cancellationToken).ConfigureAwait(false);
        }
        await SetBookStatusAsync(bookId, finalStatus, cancellationToken).ConfigureAwait(false);

        return new ExtractionBookResult(
            bookId,
            finalStatus == SearchBookIndexStatus.Indexed,
            extracted.PagesProcessed,
            extracted.PagesSkipped,
            extracted.FailedPages,
            chunksWritten,
            finalStatus == SearchBookIndexStatus.Indexed
                ? null
                : $"{extracted.FailedPages.ToString(System.Globalization.CultureInfo.InvariantCulture)} page(s) failed extraction.");
    }

    private async Task<ExtractPagesResult> ExtractPagesAsync(
        string bookId,
        string? contentHash,
        IPdfRenderer renderer,
        string extractorVersion,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ExtractedPageRecord> existingPages = await _extractedTextStore
            .ListForBookAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<int, ExtractedPageRecord> existingByPage = existingPages
            .ToDictionary(p => p.PageIndex);

        var records = new List<ExtractedPageRecord>(renderer.PageCount);
        int processed = 0;
        int skipped = 0;
        int failed = 0;

        for (int pageIndex = 0; pageIndex < renderer.PageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingByPage.TryGetValue(pageIndex, out ExtractedPageRecord? existing) &&
                string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal) &&
                string.Equals(existing.ExtractorVersion, extractorVersion, StringComparison.Ordinal) &&
                existing.Quality != SearchExtractionQuality.Failed)
            {
                records.Add(existing);
                skipped++;
                continue;
            }

            ExtractedPageRecord record;
            try
            {
                TextLayer layer = await Task.Run(
                        () => renderer.ExtractTextLayer(pageIndex),
                        cancellationToken)
                    .ConfigureAwait(false);
                string text = JoinWords(layer.Words);
                record = new ExtractedPageRecord(
                    Id: existing?.Id ?? 0,
                    BookId: bookId,
                    PageIndex: pageIndex,
                    Text: string.IsNullOrWhiteSpace(text) ? null : text,
                    Quality: MapQuality(layer.Quality),
                    WordCount: SearchChunker.CountTokens(text),
                    ContentHash: contentHash,
                    ExtractedAtUtc: DateTimeOffset.UtcNow,
                    ExtractorVersion: extractorVersion);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                record = new ExtractedPageRecord(
                    Id: existing?.Id ?? 0,
                    BookId: bookId,
                    PageIndex: pageIndex,
                    Text: null,
                    Quality: SearchExtractionQuality.Failed,
                    WordCount: 0,
                    ContentHash: contentHash,
                    ExtractedAtUtc: DateTimeOffset.UtcNow,
                    ExtractorVersion: extractorVersion);
                await RecordPageFailureAsync(bookId, contentHash, pageIndex, ex.Message, cancellationToken)
                    .ConfigureAwait(false);
            }

            ExtractedPageRecord saved = await _extractedTextStore
                .UpsertPageAsync(record, cancellationToken)
                .ConfigureAwait(false);
            records.Add(saved);
            processed++;

            if (saved.Quality == SearchExtractionQuality.Failed)
            {
                failed++;
            }
        }

        return new ExtractPagesResult(records, processed, skipped, failed);
    }

    private List<SearchChunkRecord> BuildPageChunks(
        string bookId,
        IReadOnlyList<ExtractedPageRecord> pages)
    {
        var chunks = new List<SearchChunkRecord>();
        int chunkIndex = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (ExtractedPageRecord page in pages.OrderBy(p => p.PageIndex))
        {
            if (string.IsNullOrWhiteSpace(page.Text) || page.Quality == SearchExtractionQuality.Failed)
            {
                continue;
            }

            IReadOnlyList<SearchChunkRecord> pageChunks = _chunker.Chunk(
                bookId,
                SearchChunkSource.Page,
                page.Text,
                chunkIndex,
                now,
                page.Id,
                page.PageIndex);
            chunks.AddRange(pageChunks);
            chunkIndex += pageChunks.Count;
        }

        return chunks;
    }

    private static SearchChunkRecord[] BuildTocChunks(
        string bookId,
        IReadOnlyList<TocEntryRecord> entries)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return entries
            .Select((entry, index) => new SearchChunkRecord(
                Id: 0,
                BookId: bookId,
                ExtractedPageId: null,
                PageIndex: entry.PageIndex,
                ChunkIndex: index,
                Text: $"{new string(' ', entry.Level * 2)}{entry.Title}",
                TokenCount: SearchChunker.CountTokens(entry.Title),
                Source: SearchChunkSource.Toc,
                CreatedAtUtc: now))
            .ToArray();
    }

    private async Task<IReadOnlyList<SearchChunkRecord>> BuildNoteChunksAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<string> noteTexts = await context.AnnotationsV2
            .AsNoTracking()
            .Where(a => a.BookId == bookId && a.Type == 1 && a.NoteText != null)
            .Select(a => a.NoteText!)
            .Concat(context.Annotations
                .AsNoTracking()
                .Where(a => a.BookId == bookId && a.Type == 1 && a.Body != null && a.Body.NoteText != null)
                .Select(a => a.Body!.NoteText!))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSourceChunks(bookId, SearchChunkSource.Note, noteTexts);
    }

    private async Task<IReadOnlyList<SearchChunkRecord>> BuildTagChunksAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string[] tagFields = ["tag", "tags", "Tag", "Tags", "category", "categories", "Category", "Categories"];
        List<string> tags = await context.BookMetadataFields
            .AsNoTracking()
            .Where(field => field.BookId == bookId && tagFields.Contains(field.FieldName) && field.Value != null)
            .Select(field => field.Value!)
            .Concat(context.ShelfBooks
                .AsNoTracking()
                .Where(shelfBook => shelfBook.BookId == bookId && shelfBook.Shelf != null)
                .Select(shelfBook => shelfBook.Shelf!.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSourceChunks(bookId, SearchChunkSource.Tag, tags);
    }

    private async Task<IReadOnlyList<SearchChunkRecord>> BuildDescriptionChunksAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string[] descriptionFields = ["description", "descriptions", "summary", "Description", "Descriptions", "Summary"];
        List<string> descriptions = await context.BookMetadataFields
            .AsNoTracking()
            .Where(field => field.BookId == bookId &&
                descriptionFields.Contains(field.FieldName) &&
                field.Value != null)
            .Select(field => field.Value!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSourceChunks(bookId, SearchChunkSource.Description, descriptions);
    }

    private List<SearchChunkRecord> BuildSourceChunks(
        string bookId,
        SearchChunkSource source,
        IEnumerable<string> texts)
    {
        var chunks = new List<SearchChunkRecord>();
        int chunkIndex = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (string text in texts.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            IReadOnlyList<SearchChunkRecord> next = _chunker.Chunk(
                bookId,
                source,
                text,
                chunkIndex,
                now);
            chunks.AddRange(next);
            chunkIndex += next.Count;
        }

        return chunks;
    }

    private async Task<IReadOnlyList<string>> FindPendingBookIdsAsync(
        int maxBooks,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        return await context.Books
            .AsNoTracking()
            .Where(book => book.Status == ActiveBookStatus &&
                ((book.IndexStatus != (int)SearchBookIndexStatus.Indexed &&
                  book.IndexStatus != (int)SearchBookIndexStatus.Failed) ||
                 !context.ExtractedPages.Any(page =>
                     page.BookId == book.BookId &&
                     page.ContentHash == book.Sha256Hash)))
            .OrderBy(book => book.BookId)
            .Select(book => book.BookId)
            .Take(maxBooks)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BookIndexSnapshot?> LoadBookSnapshotAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        return await context.Books
            .AsNoTracking()
            .Where(book => book.BookId == bookId && book.Status == ActiveBookStatus)
            .Select(book => new BookIndexSnapshot(book.BookId, book.Sha256Hash))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SetBookStatusAsync(
        string bookId,
        SearchBookIndexStatus status,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return;
        }

        book.IndexStatus = (int)status;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordBookFailureAsync(
        string bookId,
        string? contentHash,
        string message,
        CancellationToken cancellationToken)
    {
        await UpsertFailureJobAsync(
                bookId,
                contentHash,
                pageIndex: null,
                message,
                cancellationToken)
            .ConfigureAwait(false);
        await SetBookStatusAsync(bookId, SearchBookIndexStatus.Failed, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task RecordPageFailureAsync(
        string bookId,
        string? contentHash,
        int pageIndex,
        string message,
        CancellationToken cancellationToken) =>
        UpsertFailureJobAsync(bookId, contentHash, pageIndex, message, cancellationToken);

    private async Task UpsertFailureJobAsync(
        string bookId,
        string? contentHash,
        int? pageIndex,
        string message,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string key = ComputeIdempotencyKey(bookId, contentHash, pageIndex);
        JobRow? job = await context.Jobs
            .FirstOrDefaultAsync(j => j.IdempotencyKey == key, cancellationToken)
            .ConfigureAwait(false);
        string payload = pageIndex is null
            ? "{\"source\":\"search-extraction\"}"
            : $"{{\"source\":\"search-extraction\",\"pageIndex\":{pageIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";

        if (job is null)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = "ExtractionFailed",
                IdempotencyKey = key,
                Status = JobFailed,
                BookId = bookId,
                Payload = payload,
                ErrorMessage = TrimError(message),
                CompletedUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            job.Status = JobFailed;
            job.Payload = payload;
            job.ErrorMessage = TrimError(message);
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.RetryCount += 1;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string JoinWords(IReadOnlyList<TextWord> words) =>
        string.Join(' ', words.Select(word => word.Text).Where(text => !string.IsNullOrWhiteSpace(text)));

    private static List<SearchChunkRecord> StampChunks(
        IReadOnlyList<SearchChunkRecord> chunks,
        long extractionArtifactId,
        string indexVersion) =>
        chunks
            .Select(chunk => chunk with
            {
                ExtractionArtifactId = extractionArtifactId,
                IndexVersion = indexVersion,
            })
            .ToList();

    private static string ComputeManifestHash(
        IReadOnlyList<ExtractedPageRecord> pages,
        params IReadOnlyList<SearchChunkRecord>[] chunkSets)
    {
        var manifest = new StringBuilder();
        foreach (ExtractedPageRecord page in pages.OrderBy(page => page.PageIndex))
        {
            manifest.Append("page|")
                .Append(page.PageIndex)
                .Append('|')
                .Append(page.ContentHash)
                .Append('|')
                .Append((int)page.Quality)
                .Append('|')
                .Append(page.WordCount)
                .Append('|')
                .Append(page.ExtractorVersion)
                .Append('\n');
        }

        foreach (SearchChunkRecord chunk in chunkSets
                     .SelectMany(chunks => chunks)
                     .OrderBy(chunk => chunk.Source)
                     .ThenBy(chunk => chunk.ChunkIndex)
                     .ThenBy(chunk => chunk.ExtractedPageId))
        {
            manifest.Append("chunk|")
                .Append((int)chunk.Source)
                .Append('|')
                .Append(chunk.ChunkIndex)
                .Append('|')
                .Append(chunk.ExtractedPageId)
                .Append('|')
                .Append(chunk.Text)
                .Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static SearchExtractionQuality MapQuality(ExtractionQuality quality) =>
        quality switch
        {
            ExtractionQuality.Full => SearchExtractionQuality.Full,
            ExtractionQuality.Partial => SearchExtractionQuality.Partial,
            ExtractionQuality.Empty => SearchExtractionQuality.Empty,
            ExtractionQuality.Scanned => SearchExtractionQuality.Scanned,
            _ => SearchExtractionQuality.Failed,
        };

    private static string TrimError(string message) =>
        message.Length <= 4096 ? message : message[..4096];

    private static string ComputeIdempotencyKey(string bookId, string? contentHash, int? pageIndex)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{bookId}|ExtractionFailed|{contentHash ?? string.Empty}|{pageIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "book"}");
        return Convert.ToHexStringLower(SHA256.HashData(data))[..32];
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private sealed record ExtractPagesResult(
        IReadOnlyList<ExtractedPageRecord> Pages,
        int PagesProcessed,
        int PagesSkipped,
        int FailedPages);

    private sealed record BookIndexSnapshot(string BookId, string? ContentHash);

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
