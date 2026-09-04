using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Page-oriented catalogue search facade. Metadata is the primary path;
/// full-text is an explicit fallback when no metadata candidate exists.
/// </summary>
public sealed class CatalogueSearchService : ICatalogueSearchService
{
    private const int MaximumPageSize = 100;
    private const int MaximumQueryLength = 128;
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IMetadataSearchService _metadataSearch;
    private readonly IFtsIndexService _ftsIndex;

    /// <summary>Initializes the page-oriented catalogue search facade.</summary>
    public CatalogueSearchService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataSearchService metadataSearch,
        IFtsIndexService ftsIndex)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _metadataSearch = metadataSearch ?? throw new ArgumentNullException(nameof(metadataSearch));
        _ftsIndex = ftsIndex ?? throw new ArgumentNullException(nameof(ftsIndex));
    }

    /// <inheritdoc />
    public async Task<CatalogueSearchPage> SearchAsync(
        CatalogueSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        int page = query.Page;
        int pageSize = query.PageSize;
        if (page <= 0 || pageSize <= 0 || pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(query),
                $"Page must be positive and page size must be between 1 and {MaximumPageSize}.");
        }
        if (page > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page offset is too large.");
        }

        string text = (query.Text ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaximumQueryLength)
        {
            return EmptyPage(page, pageSize);
        }

        ParsedQuery parsed = ParseQuery(text, query.Field);
        if (parsed.Text.Length == 0)
        {
            return EmptyPage(page, pageSize);
        }

        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        string likePattern = "%" + EscapeLike(parsed.Text) + "%";
        IQueryable<BookRow> candidates = BuildCandidateQuery(context, parsed.Field, likePattern);
        int totalCount = await candidates.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount > 0)
        {
            List<BookRow> books = await candidates
                .OrderByDescending(book => book.Title == parsed.Text)
                .ThenByDescending(book => book.IsbnNormalized == parsed.Text)
                .ThenByDescending(book => book.Doi == parsed.Text)
                .ThenByDescending(book => book.Title != null && book.Title.StartsWith(parsed.Text))
                .ThenBy(book => book.Title ?? string.Empty)
                .ThenBy(book => book.BookId)
                .Skip(checked((page - 1) * pageSize))
                .Take(pageSize)
                .Include(book => book.BookAuthors)
                .ThenInclude(link => link.Author)
                .Include(book => book.MetadataFields)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            List<CatalogueSearchItem> items = books
                .Select(book => ToItem(book, parsed.Text))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.BookId, StringComparer.Ordinal)
                .ToList();
            return new CatalogueSearchPage(
                items,
                page,
                pageSize,
                totalCount,
                BuildFacets(items),
                UsedFullTextFallback: false);
        }

        IReadOnlyList<MetadataSearchResult> fuzzy = await _metadataSearch
            .SearchAsync(text, cancellationToken)
            .ConfigureAwait(false);
        if (fuzzy.Count > 0)
        {
            IReadOnlyList<MetadataSearchResult> fuzzyPage = fuzzy
                .Skip(checked((page - 1) * pageSize))
                .Take(pageSize)
                .ToList();
            List<CatalogueSearchItem> items = fuzzyPage.Select(ToItem).ToList();
            return new CatalogueSearchPage(
                items,
                page,
                pageSize,
                fuzzy.Count,
                BuildFacets(items),
                UsedFullTextFallback: false,
                Notice: "Showing a typo-tolerant metadata match.");
        }

        IReadOnlyList<FtsSearchResult> fullText = await _ftsIndex
            .SearchAsync(text, checked(page * pageSize), cancellationToken)
            .ConfigureAwait(false);
        List<CatalogueSearchItem> fallbackItems = fullText
            .GroupBy(hit => hit.BookId, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(group => ToFullTextItem(group.Key, group.ToList(), text))
            .ToList();
        return new CatalogueSearchPage(
            fallbackItems,
            page,
            pageSize,
            fullText.Select(hit => hit.BookId).Distinct(StringComparer.Ordinal).Count(),
            BuildFacets(fallbackItems),
            UsedFullTextFallback: true,
            Notice: "Metadata search found no match; showing indexed full-text results.");
    }

    private static IQueryable<BookRow> BuildCandidateQuery(
        CatalogueDbContext context,
        string? field,
        string likePattern)
    {
        IQueryable<BookRow> books = context.Books
            .AsNoTracking()
            .Where(book => book.Status == 0);
        return field switch
        {
            "title" => books.Where(book =>
                EF.Functions.Like(book.Title ?? string.Empty, likePattern, "\\") ||
                book.MetadataFields.Any(metadata => metadata.FieldName == "Title" &&
                    EF.Functions.Like(metadata.Value ?? string.Empty, likePattern, "\\"))),
            "author" => books.Where(book =>
                book.BookAuthors.Any(link => link.Author != null &&
                    EF.Functions.Like(link.Author.NormalizedName, likePattern, "\\")) ||
                book.MetadataFields.Any(metadata => metadata.FieldName == "Author" &&
                    EF.Functions.Like(metadata.Value ?? string.Empty, likePattern, "\\"))),
            "isbn" => books.Where(book =>
                EF.Functions.Like(book.IsbnNormalized ?? string.Empty, likePattern, "\\")),
            "shelf" => books.Where(book => book.ShelfBooks.Any(link => link.Shelf != null &&
                EF.Functions.Like(link.Shelf.Name, likePattern, "\\"))),
            "description" => books.Where(book => book.MetadataFields.Any(metadata =>
                (metadata.FieldName == "Description" || metadata.FieldName == "Descriptions") &&
                EF.Functions.Like(metadata.Value ?? string.Empty, likePattern, "\\"))),
            "tag" => books.Where(book => book.MetadataFields.Any(metadata =>
                (metadata.FieldName == "Tag" || metadata.FieldName == "Tags") &&
                EF.Functions.Like(metadata.Value ?? string.Empty, likePattern, "\\"))),
            _ => books.Where(book =>
                EF.Functions.Like(book.Title ?? string.Empty, likePattern, "\\") ||
                EF.Functions.Like(book.IsbnNormalized ?? string.Empty, likePattern, "\\") ||
                EF.Functions.Like(book.Doi ?? string.Empty, likePattern, "\\") ||
                book.BookAuthors.Any(link => link.Author != null &&
                    EF.Functions.Like(link.Author.NormalizedName, likePattern, "\\")) ||
                book.MetadataFields.Any(metadata =>
                    EF.Functions.Like(metadata.Value ?? string.Empty, likePattern, "\\")) ||
                book.ShelfBooks.Any(link => link.Shelf != null &&
                    EF.Functions.Like(link.Shelf.Name, likePattern, "\\"))),
        };
    }

    private static CatalogueSearchItem ToItem(BookRow book, string query)
    {
        string? author = book.BookAuthors
            .OrderBy(link => link.DisplayOrder)
            .ThenBy(link => link.Author?.NormalizedName, StringComparer.Ordinal)
            .Select(link => link.Author?.NormalizedName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var fields = new List<string>();
        int score = 0;
        if (Contains(book.Title, query))
        {
            score += string.Equals(book.Title, query, StringComparison.OrdinalIgnoreCase) ? 100 : 80;
            fields.Add(string.Equals(book.Title, query, StringComparison.OrdinalIgnoreCase) ? "title:exact" : "title");
        }
        if (Contains(author, query))
        {
            score += 60;
            fields.Add("author");
        }
        if (Contains(book.IsbnNormalized, query) || Contains(book.Doi, query))
        {
            score += 70;
            fields.Add("identifier");
        }
        if (book.MetadataFields.Any(field => IsNamed(field, "tag") && Contains(field.Value, query)))
        {
            score += 40;
            fields.Add("tag");
        }
        if (book.MetadataFields.Any(field => IsNamed(field, "description") && Contains(field.Value, query)))
        {
            score += 20;
            fields.Add("description");
        }
        return new CatalogueSearchItem(
            book.BookId,
            book.Title,
            author,
            score,
            fields,
            Highlight(book.Title, query),
            Highlight(author, query));
    }

    private static CatalogueSearchItem ToItem(MetadataSearchResult result) => new(
        result.BookId,
        result.Title,
        result.Author,
        result.Score,
        result.MatchedFields,
        Highlight(result.Title, result.CorrectionSuggestion ?? string.Empty),
        Highlight(result.Author, result.CorrectionSuggestion ?? string.Empty),
        result.CorrectionSuggestion);

    private static CatalogueSearchItem ToFullTextItem(
        string bookId,
        List<FtsSearchResult> hits,
        string query)
    {
        FtsSearchResult first = hits[0];
        return new CatalogueSearchItem(
            bookId,
            first.Title,
            first.Author,
            Math.Max(1, (int)Math.Round(first.Score * 100)),
            hits.Select(hit => "full-text:" + hit.Source.ToString().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Highlight(first.Title, query),
            Highlight(first.Author, query),
            FullTextHits: hits);
    }

    private static List<CatalogueSearchFacet> BuildFacets(IEnumerable<CatalogueSearchItem> items) => items
        .SelectMany(item => item.MatchedFields)
        .GroupBy(field => field.Split(':')[0], StringComparer.Ordinal)
        .Select(group => new CatalogueSearchFacet(group.Key, group.Count()))
        .OrderByDescending(facet => facet.Count)
        .ThenBy(facet => facet.Field, StringComparer.Ordinal)
        .ToList();

    private static SearchSnippet? Highlight(string? value, string query)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(query))
        {
            return null;
        }
        int start = value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        return start < 0
            ? null
            : new SearchSnippet(value, [new SearchSnippetSpan(start, query.Length)]);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsNamed(BookMetadataFieldRow field, string name) =>
        string.Equals(field.FieldName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(field.FieldName, name + "s", StringComparison.OrdinalIgnoreCase);

    private static ParsedQuery ParseQuery(string query, string? explicitField)
    {
        string? field = string.IsNullOrWhiteSpace(explicitField)
            ? null
            : explicitField.Trim().ToLowerInvariant();
        int separator = query.IndexOf(':');
        if (field is null && separator > 0 && separator < query.Length - 1)
        {
            string candidate = query[..separator].Trim().ToLowerInvariant();
            if (candidate is "title" or "author" or "isbn" or "shelf" or "description" or "tag")
            {
                field = candidate;
                query = query[(separator + 1)..].Trim();
            }
        }
        if (field is not null && field is not ("title" or "author" or "isbn" or "shelf" or "description" or "tag"))
        {
            field = null;
        }
        return new ParsedQuery(field, query);
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static CatalogueSearchPage EmptyPage(int page, int pageSize) =>
        new([], page, pageSize, 0, [], UsedFullTextFallback: false);

    private sealed record ParsedQuery(string? Field, string Text);
}
