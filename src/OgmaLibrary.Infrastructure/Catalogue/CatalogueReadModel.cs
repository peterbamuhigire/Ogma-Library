using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// EF Core implementation of <see cref="ICatalogueReadModel"/>.
/// Uses server-side LINQ projections to avoid loading entity graphs and to keep
/// the projected records EF-Core-free (LAN-CLASSROOM-ARCHITECTURE.md §2-3).
/// </summary>
public sealed class CatalogueReadModel : ICatalogueReadModel
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal CatalogueReadModel(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public CatalogueReadModel(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
        CatalogueFilter filter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var query = context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            string titleContains = filter.TitleContains;
            query = query.Where(b =>
                (b.Title != null && b.Title.Contains(titleContains)) ||
                b.MetadataFields.Any(f =>
                    f.FieldName == "Title" &&
                    f.Value != null &&
                    f.Value.Contains(titleContains)));
        }

        if (!string.IsNullOrWhiteSpace(filter.AuthorContains))
        {
            string authorContains = filter.AuthorContains;
            query = query.Where(b =>
                b.BookAuthors.Any(ba => ba.Author != null && ba.Author.NormalizedName.Contains(authorContains)) ||
                b.MetadataFields.Any(f =>
                    f.FieldName == "Author" &&
                    f.Value != null &&
                    f.Value.Contains(authorContains)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ShelfId))
        {
            query = query.Where(b => b.ShelfBooks.Any(sb => sb.ShelfId == filter.ShelfId));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(b => b.Status == filter.Status.Value);
        }

        var projected = query
            .OrderBy(b => b.Title ?? string.Empty)
            .Select(b => new
            {
                b.BookId,
                b.Title,
                b.Status,
                b.Rating,
                b.Year,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                ShelfIds = b.ShelfBooks
                    .Select(sb => sb.ShelfId)
                    .ToList(),
                Progress = b.ReadingProgress,
                HasPresentFile = b.BookFiles.Any(f => f.FileStatus == 0),
                MetadataFields = b.MetadataFields
                    .Where(f => f.FieldName == "Title" || f.FieldName == "Author")
                    .Select(f => new { f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden })
                    .ToList(),
            });

        if (filter.MaxResults > 0)
        {
            projected = projected.Take(filter.MaxResults);
        }

        var results = await projected.ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var item in results)
        {
            var fields = item.MetadataFields
                .Select(f => new MetadataFieldProjection(f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden))
                .ToList();

            yield return new BookSummaryProjection(
                BookId: item.BookId,
                Title: ResolveTitle(item.Title, fields),
                Authors: ResolveAuthors(item.Authors, fields),
                CoverRelativePath: null, // Phase 05 populates covers
                Status: item.Status,
                Rating: item.Rating,
                ShelfIds: item.ShelfIds,
                ReadingProgressPct: item.Progress?.CompletionPct,
                IsAvailable: item.HasPresentFile,
                Year: item.Year);
        }
    }

    /// <inheritdoc />
    public async Task<BookDetailProjection?> GetBookDetailAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var result = await context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new
            {
                b.BookId,
                b.Title,
                b.Year,
                b.IsbnNormalized,
                b.Doi,
                b.Rating,
                b.Status,
                b.RelativePath,
                b.Sha256Hash,
                b.SizeBytes,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                Progress = b.ReadingProgress,
                Memory = b.ReadingMemory,
                AnnotationCount = b.Annotations.Count,
                MetadataFields = b.MetadataFields
                    .Select(f => new { f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return null;
        }

        ReadingProgressProjection? progress = result.Progress is not null
            ? new ReadingProgressProjection(
                BookId: result.BookId,
                CurrentPage: result.Progress.CurrentPage,
                CompletionPct: result.Progress.CompletionPct,
                LastReadUtc: result.Progress.LastReadUtc,
                Status: result.Progress.Status)
            : null;

        var fields = result.MetadataFields
            .Select(f => new MetadataFieldProjection(f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden))
            .ToList();

        ReadingMemorySummaryProjection? memory = result.Memory is not null
            ? new ReadingMemorySummaryProjection(
                Disposition: result.Memory.Disposition,
                KeyInsight: result.Memory.KeyInsight,
                UpdatedAtUtc: result.Memory.UpdatedAtUtc)
            : null;

        return new BookDetailProjection(
            BookId: result.BookId,
            Title: ResolveTitle(result.Title, fields),
            Authors: ResolveAuthors(result.Authors, fields),
            Year: result.Year,
            Isbn: result.IsbnNormalized,
            Doi: result.Doi,
            Rating: result.Rating,
            Status: result.Status,
            CoverRelativePath: null,
            RelativePath: result.RelativePath,
            Sha256Hash: result.Sha256Hash,
            SizeBytes: result.SizeBytes,
            ReadingProgress: progress,
            Annotations: result.AnnotationCount,
            MetadataFields: fields,
            ReadingMemory: memory);
    }

    private static string? ResolveTitle(
        string? title,
        IReadOnlyList<MetadataFieldProjection> fields) =>
        !string.IsNullOrWhiteSpace(title)
            ? title
            : SelectBestMetadataValue(fields, "Title");

    private static List<string> ResolveAuthors(
        List<string> authors,
        IReadOnlyList<MetadataFieldProjection> fields)
    {
        if (authors.Count > 0)
        {
            return authors;
        }

        string? value = SelectBestMetadataValue(fields, "Author");
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }

    private static string? SelectBestMetadataValue(
        IReadOnlyList<MetadataFieldProjection> fields,
        string fieldName) =>
        fields
            .Where(f =>
                string.Equals(f.FieldName, fieldName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(f.Value))
            .OrderByDescending(f => f.IsOverridden)
            .ThenByDescending(f => f.Confidence ?? 0)
            .ThenBy(f => f.Source)
            .Select(f => f.Value)
            .FirstOrDefault();

    /// <inheritdoc />
    public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var shelves = await context.Shelves
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                s.ShelfId,
                s.Name,
                s.ShelfType,
                BookCount = s.ShelfBooks.Count,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var s in shelves)
        {
            yield return new ShelfProjection(
                ShelfId: s.ShelfId,
                Name: s.Name,
                IsSmart: s.ShelfType == 1,
                BookCount: s.BookCount);
        }
    }

    /// <inheritdoc />
    public async Task<ReadingProgressProjection?> GetProgressAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var result = await context.ReadingProgress
            .AsNoTracking()
            .Where(r => r.BookId == bookId)
            .Select(r => new { r.BookId, r.CurrentPage, r.CompletionPct, r.LastReadUtc, r.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? null
            : new ReadingProgressProjection(
                BookId: result.BookId,
                CurrentPage: result.CurrentPage,
                CompletionPct: result.CompletionPct,
                LastReadUtc: result.LastReadUtc,
                Status: result.Status);
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
