using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Deterministic implementation of the no-AI book metadata enrichment flow.
/// Provider calls are ordinary HTTP lookups only; no AI services or token-consuming
/// endpoints are used.
/// </summary>
public sealed class BookMetadataEnrichmentService : IBookMetadataEnrichmentService
{
    private const double AutoApplyThreshold = 0.70;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly ILibrarySettingsService _settings;
    private readonly IIsbnDetectionService _isbnDetection;
    private readonly IMetadataProviderAggregator _providerAggregator;
    private readonly IConfidenceMergeService _mergeService;
    private readonly IMetadataApplyService _applyService;
    private readonly IMetadataWriteBackService _writeBackService;

    /// <summary>Initializes a new instance of <see cref="BookMetadataEnrichmentService"/>.</summary>
    internal BookMetadataEnrichmentService(
        CatalogueDbContext context,
        ILibrarySettingsService settings,
        IIsbnDetectionService isbnDetection,
        IMetadataProviderAggregator providerAggregator,
        IConfidenceMergeService mergeService,
        IMetadataApplyService applyService,
        IMetadataWriteBackService writeBackService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(isbnDetection);
        ArgumentNullException.ThrowIfNull(providerAggregator);
        ArgumentNullException.ThrowIfNull(mergeService);
        ArgumentNullException.ThrowIfNull(applyService);
        ArgumentNullException.ThrowIfNull(writeBackService);

        _context = context;
        _settings = settings;
        _isbnDetection = isbnDetection;
        _providerAggregator = providerAggregator;
        _mergeService = mergeService;
        _applyService = applyService;
        _writeBackService = writeBackService;
    }

    /// <summary>Initializes a new instance of <see cref="BookMetadataEnrichmentService"/>.</summary>
    public BookMetadataEnrichmentService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibrarySettingsService settings,
        IIsbnDetectionService isbnDetection,
        IMetadataProviderAggregator providerAggregator,
        IConfidenceMergeService mergeService,
        IMetadataApplyService applyService,
        IMetadataWriteBackService writeBackService)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(isbnDetection);
        ArgumentNullException.ThrowIfNull(providerAggregator);
        ArgumentNullException.ThrowIfNull(mergeService);
        ArgumentNullException.ThrowIfNull(applyService);
        ArgumentNullException.ThrowIfNull(writeBackService);

        _contextFactory = contextFactory;
        _settings = settings;
        _isbnDetection = isbnDetection;
        _providerAggregator = providerAggregator;
        _mergeService = mergeService;
        _applyService = applyService;
        _writeBackService = writeBackService;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> EnrichAsync(
        string bookId,
        string? absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        try
        {
            BookRow? book;
            string? pdfPath;
            MetadataLookupRequest request;

            using (CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, cancellationToken)
                .ConfigureAwait(false))
            {
                CatalogueDbContext context = lease.Context;

                book = await context.Books
                    .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                    .Include(b => b.MetadataFields)
                    .Include(b => b.BookFiles)
                    .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
                    .ConfigureAwait(false);

                if (book is null)
                {
                    return (false, "Book not found.");
                }

                pdfPath = await ResolvePdfPathAsync(book, absoluteFilePath, cancellationToken)
                    .ConfigureAwait(false);

                request = await BuildLookupRequestAsync(book, pdfPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!request.HasAnySearchKey)
            {
                await WriteAuditAsync(bookId, "MetadataEnrichmentSkipped", new { reason = "No ISBN, title, or author search key" }, cancellationToken)
                    .ConfigureAwait(false);
                return (true, null);
            }

            IReadOnlyList<ProviderMetadataResult> results = await _providerAggregator
                .AggregateAsync(bookId, request, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<MergedMetadataProposal> proposals = await _mergeService
                .MergeAsync(bookId, results, cancellationToken)
                .ConfigureAwait(false);

            List<AcceptedFieldProposal> accepted = proposals
                .Where(p => !string.IsNullOrWhiteSpace(p.ProposedValue))
                .Where(p => p.MergedConfidence >= AutoApplyThreshold)
                .Where(p => !string.Equals(p.WinningProvider, "UserOverride", StringComparison.OrdinalIgnoreCase))
                .Where(p => !string.Equals(p.ProposedValue, p.CurrentValue, StringComparison.Ordinal))
                .Select(p => new AcceptedFieldProposal(
                    p.FieldName,
                    p.ProposedValue,
                    p.WinningProvider,
                    p.MergedConfidence,
                    IsOverridden: false))
                .ToList();

            if (accepted.Count == 0)
            {
                await WriteAuditAsync(bookId, "MetadataEnrichmentNoChanges", new
                {
                    request,
                    providerResults = results.Count,
                }, cancellationToken).ConfigureAwait(false);
                return (true, null);
            }

            await _applyService.ApplyMergedMetadataAsync(bookId, accepted, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(pdfPath))
            {
                await TryWriteBackAsync(bookId, pdfPath, accepted, cancellationToken)
                    .ConfigureAwait(false);
            }

            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<MetadataLookupRequest> BuildLookupRequestAsync(
        BookRow book,
        string? pdfPath,
        CancellationToken cancellationToken)
    {
        string? isbn = FirstMetadataValue(book, "ISBN") ?? book.IsbnNormalized;

        if (string.IsNullOrWhiteSpace(isbn) && !string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
        {
            IsbnDetectionResult detection = await _isbnDetection
                .DetectAsync(pdfPath, cancellationToken)
                .ConfigureAwait(false);

            isbn = detection.BestIsbn?.Normalized;
        }

        string? title = book.Title ?? FirstMetadataValue(book, "Title");
        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(pdfPath))
        {
            title = Path.GetFileNameWithoutExtension(pdfPath)
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();
        }

        string? author = book.BookAuthors
            .OrderBy(ba => ba.DisplayOrder)
            .Select(ba => ba.Author?.NormalizedName)
            .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ??
            FirstMetadataValue(book, "Author");

        return new MetadataLookupRequest(
            Isbn13: string.IsNullOrWhiteSpace(isbn) ? null : isbn,
            Title: string.IsNullOrWhiteSpace(title) ? null : title,
            Author: string.IsNullOrWhiteSpace(author) ? null : author);
    }

    private async Task<string?> ResolvePdfPathAsync(
        BookRow book,
        string? absoluteFilePath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(absoluteFilePath) && Path.IsPathFullyQualified(absoluteFilePath))
        {
            return absoluteFilePath;
        }

        string? libraryRoot = await _settings.GetLibraryRootAsync(cancellationToken)
            .ConfigureAwait(false);

        string? relativePath = book.BookFiles
            .Where(f => f.FileStatus == 0)
            .Select(f => f.RelativePath)
            .FirstOrDefault() ?? book.RelativePath;

        if (string.IsNullOrWhiteSpace(libraryRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return absoluteFilePath;
        }

        return Path.GetFullPath(Path.Combine(
            libraryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private async Task TryWriteBackAsync(
        string bookId,
        string pdfPath,
        IReadOnlyList<AcceptedFieldProposal> accepted,
        CancellationToken cancellationToken)
    {
        if (!CanAttemptWrite(pdfPath))
        {
            await WriteAuditAsync(bookId, "WriteBackSkipped", new
            {
                path = pdfPath,
                reason = "File is not writable or does not exist",
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        List<AcceptedFieldProposal> writableFields = accepted
            .Where(p => p.FieldName is "Title" or "Author" or "Publisher" or "Description" or "Categories")
            .Select(p => p.FieldName == "Categories"
                ? p with { FieldName = "Keywords" }
                : p)
            .ToList();

        if (writableFields.Count == 0)
        {
            return;
        }

        try
        {
            BackupToken backup = await _writeBackService
                .PrepareBackupAsync(bookId, pdfPath, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<FieldDiff> diff = await _writeBackService
                .BuildDiffAsync(pdfPath, writableFields, cancellationToken)
                .ConfigureAwait(false);

            if (diff.Count == 0)
            {
                return;
            }

            bool written = await _writeBackService
                .WriteAsync(bookId, writableFields, backup, cancellationToken)
                .ConfigureAwait(false);

            if (!written)
            {
                await WriteAuditAsync(bookId, "WriteBackSkipped", new
                {
                    path = pdfPath,
                    reason = "Write-back failed and original was restored",
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await WriteAuditAsync(bookId, "WriteBackSkipped", new
            {
                path = pdfPath,
                reason = ex.Message,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteAuditAsync(
        string bookId,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = eventType,
            EntityId = bookId,
            EntityType = "Book",
            AfterJson = JsonSerializer.Serialize(payload),
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool CanAttemptWrite(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            FileAttributes attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                return false;
            }

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read);
            return stream.CanWrite;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? FirstMetadataValue(BookRow book, string fieldName) =>
        book.MetadataFields
            .Where(f => string.Equals(f.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Confidence ?? 0.0)
            .Select(f => f.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
