using Microsoft.Data.Sqlite;
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
    private readonly CatalogueMigrator? _migrator;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogue projections before retrying.</param>
    internal CatalogueReadModel(CatalogueDbContext context, CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _migrator = migrator;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogue projections before retrying.</param>
    [ActivatorUtilitiesConstructor]
    public CatalogueReadModel(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _migrator = migrator;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
        CatalogueFilter filter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BookSummaryProjection> summaries = await RunWithSchemaRepairRetryAsync(
            () => GetBookSummariesCoreAsync(filter, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        foreach (BookSummaryProjection summary in summaries)
        {
            yield return summary;
        }
    }

    private async Task<IReadOnlyList<BookSummaryProjection>> GetBookSummariesCoreAsync(
        CatalogueFilter filter,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var query = context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            string titleContains = filter.TitleContains;
            query = query.Where(b =>
                (b.Title != null && b.Title.Contains(titleContains)) ||
                b.BookFiles.Any(f => f.RelativePath.Contains(titleContains)) ||
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
                b.Sha256Hash,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                ShelfIds = b.ShelfBooks
                    .Select(sb => sb.ShelfId)
                    .ToList(),
                CoverRelativePath = b.VisualAssets
                    .Where(asset => asset.Kind == (int)VisualAssetKind.Cover &&
                                    asset.Status == (int)VisualAssetStatus.Ready)
                    .OrderByDescending(asset => asset.IsCustom)
                    .ThenBy(asset => asset.Variant)
                    .Select(asset => asset.RelativePath)
                    .FirstOrDefault(),
                Progress = b.ReadingProgress,
                HasPresentFile = b.BookFiles.Any(f => f.FileStatus == 0),
                PrimaryRelativePath = b.BookFiles
                    .OrderBy(f => f.FileStatus)
                    .ThenBy(f => f.BookFileId)
                    .Select(f => f.RelativePath)
                    .FirstOrDefault(),
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

        var summaries = new List<BookSummaryProjection>(results.Count);
        foreach (var item in results)
        {
            var fields = item.MetadataFields
                .Select(f => new MetadataFieldProjection(f.FieldName, f.Value, f.Source, f.Confidence, f.IsOverridden))
                .ToList();

            summaries.Add(new BookSummaryProjection(
                BookId: item.BookId,
                Title: ResolveTitle(item.Title, fields, item.PrimaryRelativePath, item.BookId),
                Authors: ResolveAuthors(item.Authors, fields),
                CoverRelativePath: item.CoverRelativePath,
                Status: item.Status,
                Rating: item.Rating,
                ShelfIds: item.ShelfIds,
                ReadingProgressPct: item.Progress?.CompletionPct,
                IsAvailable: item.HasPresentFile,
                Year: item.Year,
                Sha256Hash: item.Sha256Hash));
        }

        return summaries;
    }

    /// <inheritdoc />
    public async Task<BookDetailProjection?> GetBookDetailAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        return await RunWithSchemaRepairRetryAsync(
            () => GetBookDetailCoreAsync(bookId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<BookDetailProjection?> GetBookDetailCoreAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
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
                PrimaryRelativePath = b.BookFiles
                    .OrderBy(f => f.FileStatus)
                    .ThenBy(f => f.BookFileId)
                    .Select(f => f.RelativePath)
                    .FirstOrDefault(),
                CoverRelativePath = b.VisualAssets
                    .Where(asset => asset.Kind == (int)VisualAssetKind.Cover &&
                                    asset.Status == (int)VisualAssetStatus.Ready)
                    .OrderByDescending(asset => asset.IsCustom)
                    .ThenBy(asset => asset.Variant)
                    .Select(asset => asset.RelativePath)
                    .FirstOrDefault(),
                b.Sha256Hash,
                b.SizeBytes,
                b.IsOcrDerived,
                b.IsPasswordProtected,
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
            Title: ResolveTitle(result.Title, fields, result.PrimaryRelativePath ?? result.RelativePath, result.BookId),
            Authors: ResolveAuthors(result.Authors, fields),
            Year: result.Year,
            Isbn: result.IsbnNormalized,
            Doi: result.Doi,
            Rating: result.Rating,
            Status: result.Status,
            CoverRelativePath: result.CoverRelativePath,
            RelativePath: result.RelativePath ?? result.PrimaryRelativePath,
            Sha256Hash: result.Sha256Hash,
            SizeBytes: result.SizeBytes,
            ReadingProgress: progress,
            Annotations: result.AnnotationCount,
            MetadataFields: fields,
            ReadingMemory: memory,
            IsOcrDerived: result.IsOcrDerived,
            IsPasswordProtected: result.IsPasswordProtected);
    }

    private static string ResolveTitle(
        string? title,
        IReadOnlyList<MetadataFieldProjection> fields,
        string? relativePath,
        string bookId)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        string? metadataTitle = SelectBestMetadataValue(fields, "Title");
        if (!string.IsNullOrWhiteSpace(metadataTitle))
        {
            return metadataTitle.Trim();
        }

        string? fileTitle = ResolveFileTitle(relativePath);
        return string.IsNullOrWhiteSpace(fileTitle) ? bookId : fileTitle;
    }

    private static string? ResolveFileTitle(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string normalized = relativePath.Replace('\\', '/');
        string? fileName = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(withoutExtension) ? fileName : withoutExtension;
    }

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
        IReadOnlyList<ShelfProjection> shelves = await RunWithSchemaRepairRetryAsync(
            () => GetShelvesCoreAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        foreach (ShelfProjection shelf in shelves)
        {
            yield return shelf;
        }
    }

    private async Task<IReadOnlyList<ShelfProjection>> GetShelvesCoreAsync(CancellationToken cancellationToken)
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

        return shelves
            .Select(s => new ShelfProjection(
                ShelfId: s.ShelfId,
                Name: s.Name,
                IsSmart: s.ShelfType == 1,
                BookCount: s.BookCount))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ReadingProgressProjection?> GetProgressAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        return await RunWithSchemaRepairRetryAsync(
            () => GetProgressCoreAsync(bookId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReadingProgressProjection?> GetProgressCoreAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
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

    private async Task<T> RunWithSchemaRepairRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (_migrator is not null && IsMissingSqliteTable(ex))
        {
            await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
            return await action().ConfigureAwait(false);
        }
    }

    private static bool IsMissingSqliteTable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite &&
                sqlite.SqliteErrorCode == 1 &&
                sqlite.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
