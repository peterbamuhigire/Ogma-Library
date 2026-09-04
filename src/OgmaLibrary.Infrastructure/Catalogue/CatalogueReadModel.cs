using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

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
    private readonly IIdentityGroupingService? _identityGrouping;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogue projections before retrying.</param>
    /// <param name="identityGrouping">Optional reviewed identity-group projection.</param>
    internal CatalogueReadModel(
        CatalogueDbContext context,
        CatalogueMigrator? migrator = null,
        IIdentityGroupingService? identityGrouping = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _migrator = migrator;
        _identityGrouping = identityGrouping;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueReadModel"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="migrator">Optional schema migrator used to repair damaged catalogue projections before retrying.</param>
    /// <param name="identityGrouping">Optional reviewed identity-group projection.</param>
    [ActivatorUtilitiesConstructor]
    public CatalogueReadModel(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        CatalogueMigrator? migrator = null,
        IIdentityGroupingService? identityGrouping = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _migrator = migrator;
        _identityGrouping = identityGrouping;
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
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.MaxResults < 0 || filter.SkipCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Catalogue paging values cannot be negative.");
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var query = context.Books.AsNoTracking();
        IReadOnlyList<SmartShelfCondition>? smartConditions = null;

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
            var shelf = await context.Shelves
                .AsNoTracking()
                .Where(s => s.ShelfId == filter.ShelfId)
                .Select(s => new { s.ShelfType, s.Query })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (shelf is null)
            {
                return [];
            }

            if (shelf.ShelfType == 1)
            {
                if (!SmartShelfQueryParser.TryParse(shelf.Query, out smartConditions))
                {
                    // A damaged or untrusted saved query must never broaden a shelf
                    // to the full catalogue. It fails closed to an empty result.
                    return [];
                }

                query = ApplySmartShelfConditions(query, smartConditions);
            }
            else
            {
                query = query.Where(b => b.ShelfBooks.Any(sb => sb.ShelfId == filter.ShelfId));
            }
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
                b.IsFavourite,
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

        if (smartConditions is null && filter.SkipCount > 0)
        {
            projected = projected.Skip(filter.SkipCount);
        }

        if (smartConditions is null && filter.MaxResults > 0)
        {
            projected = projected.Take(filter.MaxResults);
        }

        var results = await projected.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (_identityGrouping is not null && results.Count > 1)
        {
            IReadOnlyList<IdentityGroupBookMembership> memberships = await _identityGrouping
                .FindBookMembershipsAsync(
                    results.Select(item => item.BookId).ToArray(),
                    includeWorkGroups: false,
                    cancellationToken)
                .ConfigureAwait(false);
            HashSet<string> hiddenBookIds = memberships
                .GroupBy(item => item.GroupId, StringComparer.Ordinal)
                .SelectMany(group => group
                    .OrderBy(item => item.BookId, StringComparer.Ordinal)
                    .Skip(1))
                .Select(item => item.BookId)
                .ToHashSet(StringComparer.Ordinal);
            if (hiddenBookIds.Count > 0)
            {
                results = results.Where(item => !hiddenBookIds.Contains(item.BookId)).ToList();
            }
        }

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
                IsFavourite: item.IsFavourite,
                ShelfIds: item.ShelfIds,
                ReadingProgressPct: item.Progress?.CompletionPct,
                IsAvailable: item.HasPresentFile,
                Year: item.Year,
                Sha256Hash: item.Sha256Hash,
                RelativePath: item.PrimaryRelativePath));
        }

        if (smartConditions is not null)
        {
            summaries = SmartShelfEvaluator.Evaluate(summaries, smartConditions).ToList();
            if (filter.SkipCount > 0)
            {
                summaries = summaries.Skip(filter.SkipCount).ToList();
            }

            if (filter.MaxResults > 0)
            {
                summaries = summaries.Take(filter.MaxResults).ToList();
            }
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
                b.IsFavourite,
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
                 HasPresentFile = b.BookFiles.Any(f => f.FileStatus == 0),
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
             IsPasswordProtected: result.IsPasswordProtected,
             IsFavourite: result.IsFavourite,
             IsAvailable: result.HasPresentFile);
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
                s.Query,
                BookCount = s.ShelfBooks.Count,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var projections = new List<ShelfProjection>(shelves.Count);
        foreach (var shelf in shelves)
        {
            int bookCount = shelf.ShelfType == 1
                ? await GetSmartShelfBookCountAsync(context, shelf.Query, cancellationToken)
                    .ConfigureAwait(false)
                : shelf.BookCount;

            projections.Add(new ShelfProjection(
                ShelfId: shelf.ShelfId,
                Name: shelf.Name,
                IsSmart: shelf.ShelfType == 1,
                BookCount: bookCount));
        }

        return projections;
    }

    private static async Task<int> GetSmartShelfBookCountAsync(
        CatalogueDbContext context,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!SmartShelfQueryParser.TryParse(query, out IReadOnlyList<SmartShelfCondition>? conditions))
        {
            return 0;
        }

        var books = context.Books
            .AsNoTracking();

        books = ApplySmartShelfConditions(books, conditions);
        var rows = await books
            .Select(book => new
            {
                book.BookId,
                book.Status,
                book.Rating,
                book.Year,
                HasPresentFile = book.BookFiles.Any(file => file.FileStatus == 0),
                ProgressPct = book.ReadingProgress == null
                    ? null
                    : (double?)book.ReadingProgress.CompletionPct,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summaries = rows.Select(book => new BookSummaryProjection(
            BookId: book.BookId,
            Title: null,
            Authors: [],
            CoverRelativePath: null,
            Status: book.Status,
            Rating: book.Rating,
            ShelfIds: [],
            ReadingProgressPct: book.ProgressPct,
            IsAvailable: book.HasPresentFile,
            Year: book.Year));

        return SmartShelfEvaluator.Evaluate(summaries, conditions).Count;
    }

    private static IQueryable<BookRow> ApplySmartShelfConditions(
        IQueryable<BookRow> query,
        IReadOnlyList<SmartShelfCondition> conditions)
    {
        foreach (SmartShelfCondition condition in conditions)
        {
            if (condition.Field == SmartShelfField.IsAvailable)
            {
                bool expected = bool.Parse(condition.Value);
                Expression<Func<BookRow, bool>> available = book =>
                    book.BookFiles.Any(file => file.FileStatus == 0);
                query = ApplyBooleanCondition(query, available, condition.Operator, expected);
                continue;
            }

            double value = double.Parse(
                condition.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
            Expression<Func<BookRow, double?>> field = condition.Field switch
            {
                SmartShelfField.Rating => book => book.Rating,
                SmartShelfField.Status => book => book.Status,
                SmartShelfField.Year => book => book.Year,
                SmartShelfField.ReadingProgressPct => book => book.ReadingProgress == null
                    ? null
                    : book.ReadingProgress.CompletionPct,
                _ => throw new InvalidOperationException("Unsupported smart-shelf field."),
            };
            query = ApplyNumericCondition(query, field, condition.Operator, value);
        }

        return query;
    }

    private static IQueryable<BookRow> ApplyBooleanCondition(
        IQueryable<BookRow> query,
        Expression<Func<BookRow, bool>> field,
        SmartShelfOperator operatorKind,
        bool expected)
    {
        ParameterExpression parameter = field.Parameters[0];
        Expression comparison = operatorKind == SmartShelfOperator.Equals
            ? Expression.Equal(field.Body, Expression.Constant(expected))
            : Expression.NotEqual(field.Body, Expression.Constant(expected));
        return query.Where(Expression.Lambda<Func<BookRow, bool>>(comparison, parameter));
    }

    private static IQueryable<BookRow> ApplyNumericCondition(
        IQueryable<BookRow> query,
        Expression<Func<BookRow, double?>> field,
        SmartShelfOperator operatorKind,
        double expected)
    {
        ParameterExpression parameter = field.Parameters[0];
        MemberExpression hasValue = Expression.Property(field.Body, nameof(Nullable<double>.HasValue));
        UnaryExpression value = Expression.Convert(
            Expression.Property(field.Body, nameof(Nullable<double>.Value)),
            typeof(double));
        ConstantExpression target = Expression.Constant(expected);
        BinaryExpression comparison = operatorKind switch
        {
            SmartShelfOperator.Equals => Expression.Equal(value, target),
            SmartShelfOperator.NotEquals => Expression.NotEqual(value, target),
            SmartShelfOperator.GreaterThan => Expression.GreaterThan(value, target),
            SmartShelfOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(value, target),
            SmartShelfOperator.LessThan => Expression.LessThan(value, target),
            SmartShelfOperator.LessThanOrEqual => Expression.LessThanOrEqual(value, target),
            _ => throw new InvalidOperationException("Unsupported smart-shelf operator."),
        };
        return query.Where(Expression.Lambda<Func<BookRow, bool>>(
            Expression.AndAlso(hasValue, comparison),
            parameter));
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
