using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// EF Core implementation of <see cref="IMetadataSearchService"/>. It uses the
/// catalogue as the source of truth and computes deterministic relevance scores
/// over title, author, ISBN/DOI, tags, descriptions, and shelves.
/// </summary>
public sealed class MetadataSearchService : IMetadataSearchService
{
    private const int MaxResults = 50;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataSearchService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal MetadataSearchService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataSearchService"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    [ActivatorUtilitiesConstructor]
    public MetadataSearchService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = NormalizeQuery(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string likePattern = "%" + EscapeLike(normalizedQuery) + "%";

        List<BookRow> books = await context.Books
            .AsNoTracking()
            .Where(book =>
                EF.Functions.Like(book.Title ?? string.Empty, likePattern, "\\") ||
                EF.Functions.Like(book.IsbnNormalized ?? string.Empty, likePattern, "\\") ||
                EF.Functions.Like(book.Doi ?? string.Empty, likePattern, "\\") ||
                book.BookAuthors.Any(author =>
                    author.Author != null &&
                    EF.Functions.Like(author.Author.NormalizedName, likePattern, "\\")) ||
                book.MetadataFields.Any(field =>
                    EF.Functions.Like(field.Value ?? string.Empty, likePattern, "\\")) ||
                book.ShelfBooks.Any(shelfBook =>
                    shelfBook.Shelf != null &&
                    EF.Functions.Like(shelfBook.Shelf.Name, likePattern, "\\")))
            .Include(b => b.BookAuthors)
            .ThenInclude(ba => ba.Author)
            .Include(b => b.MetadataFields)
            .Include(b => b.ShelfBooks)
            .ThenInclude(sb => sb.Shelf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return books
            .Select(book => ScoreBook(book, normalizedQuery))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.BookId, StringComparer.Ordinal)
            .Take(MaxResults)
            .ToList();
    }

    private static MetadataSearchResult ScoreBook(BookRow book, string query)
    {
        int score = 0;
        var matchedFields = new List<string>();
        string? title = book.Title;
        string? firstAuthor = book.BookAuthors
            .OrderBy(author => author.DisplayOrder)
            .Select(author => author.Author?.NormalizedName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        if (EqualsIgnoreCase(title, query))
        {
            score += 100;
            matchedFields.Add("title:exact");
        }

        if (StartsWithIgnoreCase(title, query))
        {
            score += 80;
            matchedFields.Add("title:prefix");
        }
        else if (ContainsIgnoreCase(title, query))
        {
            score += 50;
            matchedFields.Add("title");
        }

        if (book.BookAuthors.Any(author => ContainsIgnoreCase(author.Author?.NormalizedName, query)))
        {
            score += 60;
            matchedFields.Add("author");
        }

        if (ContainsIgnoreCase(book.IsbnNormalized, query) || ContainsIgnoreCase(book.Doi, query))
        {
            score += 70;
            matchedFields.Add("identifier");
        }

        if (book.MetadataFields.Any(field =>
                IsField(field, "tag") && ContainsIgnoreCase(field.Value, query)))
        {
            score += 40;
            matchedFields.Add("tag");
        }

        if (book.ShelfBooks.Any(shelfBook => ContainsIgnoreCase(shelfBook.Shelf?.Name, query)))
        {
            score += 30;
            matchedFields.Add("shelf");
        }

        if (book.MetadataFields.Any(field =>
                IsField(field, "description") && ContainsIgnoreCase(field.Value, query)))
        {
            score += 20;
            matchedFields.Add("description");
        }

        return new MetadataSearchResult(
            book.BookId,
            title,
            firstAuthor,
            score,
            matchedFields);
    }

    private static string NormalizeQuery(string? query) => (query ?? string.Empty).Trim();

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static bool IsField(BookMetadataFieldRow field, string fieldName) =>
        string.Equals(field.FieldName, fieldName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(field.FieldName, fieldName + "s", StringComparison.OrdinalIgnoreCase);

    private static bool EqualsIgnoreCase(string? value, string query) =>
        string.Equals(value, query, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithIgnoreCase(string? value, string query) =>
        value?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsIgnoreCase(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

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
