using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Reader;

/// <summary>
/// Exports and imports local reader state as a small, versioned JSON document.
/// Import is same-book only, bounded to 8 MiB, and idempotent by stable bookmark
/// and annotation identifiers.
/// </summary>
public sealed class ReaderPortabilityService : IReaderPortabilityService
{
    private const int CurrentSchemaVersion = 1;
    private const int MaxDocumentBytes = 8 * 1024 * 1024;
    private const int MaxBookmarks = 10_000;
    private const int MaxAnnotations = 2_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Creates a service backed by an explicit context for tests.</summary>
    public ReaderPortabilityService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Creates a service backed by independent contexts for production.</summary>
    public ReaderPortabilityService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task ExportAsync(string bookId, Stream destination, CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        bool bookExists = await context.Books.AnyAsync(book => book.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (!bookExists)
        {
            throw new KeyNotFoundException($"Book '{bookId}' was not found.");
        }

        var document = new ReaderStateDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            BookId = bookId,
            Progress = await context.ReadingProgress.AsNoTracking()
                .Where(progress => progress.BookId == bookId)
                .Select(progress => new ProgressDto
                {
                    CurrentPage = progress.CurrentPage,
                    ScrollOffsetPx = progress.ScrollOffsetPx,
                    LastReadUtc = progress.LastReadUtc,
                    TotalPagesRead = progress.TotalPagesRead,
                    CompletionPct = progress.CompletionPct,
                    Status = progress.Status,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            ReadingMemory = await context.ReadingMemory.AsNoTracking()
                .Where(memory => memory.BookId == bookId)
                .Select(memory => new ReadingMemoryDto
                {
                    OpenedBecause = memory.OpenedBecause,
                    KeyInsight = memory.KeyInsight,
                    OpenQuestions = memory.OpenQuestions,
                    Disposition = memory.Disposition,
                    CreatedAtUtc = memory.CreatedAtUtc,
                    UpdatedAtUtc = memory.UpdatedAtUtc,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            Bookmarks = await context.Bookmarks.AsNoTracking()
                .Where(bookmark => bookmark.BookId == bookId)
                .OrderBy(bookmark => bookmark.BookmarkId)
                .Select(bookmark => new BookmarkDto
                {
                    BookmarkId = bookmark.BookmarkId,
                    Page = bookmark.Page,
                    Label = bookmark.Label,
                    CreatedUtc = bookmark.CreatedUtc,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            Annotations = await context.AnnotationsV2.AsNoTracking()
                .Where(annotation => annotation.BookId == bookId)
                .OrderBy(annotation => annotation.AnnotationId)
                .Select(annotation => new AnnotationDto
                {
                    AnnotationId = annotation.AnnotationId,
                    LayerId = annotation.LayerId,
                    CoordinateVersion = annotation.CoordinateVersion,
                    Type = annotation.Type,
                    RegionsJson = annotation.RegionsJson,
                    ColorKey = annotation.ColorKey,
                    QuoteText = annotation.QuoteText,
                    NoteText = annotation.NoteText,
                    CreatedUtc = annotation.CreatedUtc,
                    ModifiedUtc = annotation.ModifiedUtc,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
        };

        await JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReaderImportResult> ImportAsync(
        string bookId,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        }

        using var bounded = new MemoryStream();
        await source.CopyToAsync(bounded, MaxDocumentBytes + 1, cancellationToken).ConfigureAwait(false);
        if (bounded.Length > MaxDocumentBytes)
        {
            throw new InvalidDataException("Reader export exceeds the 8 MiB safety limit.");
        }

        bounded.Position = 0;
        ReaderStateDocument document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<ReaderStateDocument>(
                bounded, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Reader export is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Reader export is not valid JSON.", ex);
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException("Reader export schema version is not supported.");
        }

        if (!string.Equals(document.BookId, bookId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Reader export belongs to a different book.");
        }

        if ((document.Bookmarks?.Count ?? 0) > MaxBookmarks ||
            (document.Annotations?.Count ?? 0) > MaxAnnotations)
        {
            throw new InvalidDataException("Reader export contains too many reader-state entries.");
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        if (!await context.Books.AnyAsync(book => book.BookId == bookId, cancellationToken).ConfigureAwait(false))
        {
            throw new KeyNotFoundException($"Book '{bookId}' was not found.");
        }

        bool progressApplied = false;
        if (document.Progress is not null)
        {
            ReadingProgressRow? progress = await context.ReadingProgress
                .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);
            if (progress is null)
            {
                progress = new ReadingProgressRow { BookId = bookId };
                context.ReadingProgress.Add(progress);
            }

            progress.CurrentPage = Math.Max(0, document.Progress.CurrentPage);
            progress.ScrollOffsetPx = Math.Max(0, document.Progress.ScrollOffsetPx);
            progress.LastReadUtc = document.Progress.LastReadUtc;
            progress.TotalPagesRead = Math.Max(0, document.Progress.TotalPagesRead);
            progress.CompletionPct = Math.Clamp(document.Progress.CompletionPct, 0, 100);
            progress.Status = document.Progress.Status is >= 0 and <= 3 ? document.Progress.Status : 0;
            progressApplied = true;
        }

        bool memoryApplied = false;
        if (document.ReadingMemory is not null)
        {
            ReadingMemoryRow? memory = await context.ReadingMemory
                .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);
            if (memory is null)
            {
                memory = new ReadingMemoryRow { BookId = bookId, CreatedAtUtc = DateTimeOffset.UtcNow };
                context.ReadingMemory.Add(memory);
            }

            memory.OpenedBecause = Limit(document.ReadingMemory.OpenedBecause, 8192);
            memory.KeyInsight = Limit(document.ReadingMemory.KeyInsight, 8192);
            memory.OpenQuestions = Limit(document.ReadingMemory.OpenQuestions, 8192);
            memory.Disposition = document.ReadingMemory.Disposition is >= 1 and <= 5
                ? document.ReadingMemory.Disposition
                : null;
            memory.UpdatedAtUtc = document.ReadingMemory.UpdatedAtUtc;
            memoryApplied = true;
        }

        int bookmarksApplied = 0;
        foreach (BookmarkDto bookmark in document.Bookmarks ?? [])
        {
            if (bookmark.Page < 0 || bookmark.BookmarkId <= 0)
            {
                continue;
            }

            BookmarkRow? row = await context.Bookmarks.FirstOrDefaultAsync(
                candidate => candidate.BookmarkId == bookmark.BookmarkId && candidate.BookId == bookId,
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                row = new BookmarkRow { BookmarkId = bookmark.BookmarkId, BookId = bookId };
                context.Bookmarks.Add(row);
            }

            row.Page = bookmark.Page;
            row.Label = Limit(bookmark.Label, 512);
            row.CreatedUtc = bookmark.CreatedUtc;
            bookmarksApplied++;
        }

        int annotationsApplied = 0;
        foreach (AnnotationDto annotation in document.Annotations ?? [])
        {
            if (string.IsNullOrWhiteSpace(annotation.AnnotationId) || annotation.AnnotationId.Length > 26)
            {
                continue;
            }

            string coordinateVersion = AnnotationCoordinateContract.NormalizeVersion(
                annotation.CoordinateVersion);
            if (!AnnotationCoordinateContract.IsSupported(coordinateVersion))
            {
                continue;
            }

            AnnotationV2Row? row = await context.AnnotationsV2.FirstOrDefaultAsync(
                candidate => candidate.AnnotationId == annotation.AnnotationId && candidate.BookId == bookId,
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                row = new AnnotationV2Row { AnnotationId = annotation.AnnotationId, BookId = bookId };
                context.AnnotationsV2.Add(row);
            }

            row.LayerId = annotation.LayerId;
            row.CoordinateVersion = coordinateVersion;
            row.Type = annotation.Type is 0 or 1 ? annotation.Type : 0;
            row.RegionsJson = Limit(annotation.RegionsJson, 65536) ?? "[]";
            row.ColorKey = Limit(annotation.ColorKey, 32);
            row.QuoteText = Limit(annotation.QuoteText, 8192);
            row.NoteText = Limit(annotation.NoteText, 65536);
            row.CreatedUtc = annotation.CreatedUtc;
            row.ModifiedUtc = annotation.ModifiedUtc;
            annotationsApplied++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReaderImportResult(progressApplied, memoryApplied, bookmarksApplied, annotationsApplied);
    }

    private static void ValidateBookId(string bookId) => ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

    private static string? Limit(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

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

    private sealed class ReaderStateDocument
    {
        public int SchemaVersion { get; set; }
        public string BookId { get; set; } = string.Empty;
        public ProgressDto? Progress { get; set; }
        public ReadingMemoryDto? ReadingMemory { get; set; }
        public List<BookmarkDto> Bookmarks { get; set; } = [];
        public List<AnnotationDto> Annotations { get; set; } = [];
    }

    private sealed class ProgressDto
    {
        public int CurrentPage { get; set; }
        public double ScrollOffsetPx { get; set; }
        public DateTimeOffset? LastReadUtc { get; set; }
        public int TotalPagesRead { get; set; }
        public double CompletionPct { get; set; }
        public int Status { get; set; }
    }

    private sealed class ReadingMemoryDto
    {
        public string? OpenedBecause { get; set; }
        public string? KeyInsight { get; set; }
        public string? OpenQuestions { get; set; }
        public int? Disposition { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class BookmarkDto
    {
        public long BookmarkId { get; set; }
        public int Page { get; set; }
        public string? Label { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
    }

    private sealed class AnnotationDto
    {
        public string AnnotationId { get; set; } = string.Empty;
        public string? LayerId { get; set; }
        public string? CoordinateVersion { get; set; }
        public int Type { get; set; }
        public string? RegionsJson { get; set; }
        public string? ColorKey { get; set; }
        public string? QuoteText { get; set; }
        public string? NoteText { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset ModifiedUtc { get; set; }
    }
}
