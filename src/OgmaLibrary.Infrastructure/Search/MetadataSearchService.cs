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
    private const int MaxQueryLength = 128;
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
        if (normalizedQuery.Length == 0 || normalizedQuery.Length > MaxQueryLength)
        {
            return [];
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string likePattern = "%" + EscapeLike(normalizedQuery) + "%";

        List<BookRow> books = await context.Books
            .AsNoTracking()
            // Search is also consumed by the classroom Host; inactive catalogue
            // records must never become discoverable through that boundary.
            .Where(book => book.Status == 0)
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

        List<MetadataSearchResult> exactResults = books
            .Select(book => ScoreBook(book, normalizedQuery))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.BookId, StringComparer.Ordinal)
            .Take(MaxResults)
            .ToList();

        if (exactResults.Count > 0)
        {
            return exactResults;
        }

        return await FuzzyFallbackAsync(context, normalizedQuery, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<MetadataSearchResult>> FuzzyFallbackAsync(
        CatalogueDbContext context,
        string query,
        CancellationToken cancellationToken)
    {
        // Select only title/first-author scalars for the bounded fallback. This
        // avoids materializing complete entity graphs and is used only after the
        // indexed/literal path found no result.
        var candidates = await context.Books
            .AsNoTracking()
            .Where(book => book.Status == 0)
            .Select(book => new
            {
                book.BookId,
                book.Title,
                Author = book.BookAuthors
                    .OrderBy(author => author.DisplayOrder)
                    .Select(author => author.Author!.NormalizedName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Select(candidate =>
            {
                int titleDistance = LevenshteinDistance(candidate.Title, query);
                int authorDistance = LevenshteinDistance(candidate.Author, query);
                int distance = Math.Min(titleDistance, authorDistance);
                string field = titleDistance <= authorDistance ? "title:fuzzy" : "author:fuzzy";
                string? matchedValue = titleDistance <= authorDistance ? candidate.Title : candidate.Author;
                return new
                {
                    candidate.BookId,
                    candidate.Title,
                    candidate.Author,
                    distance,
                    field,
                    matchedValue,
                };
            })
            .Where(candidate => candidate.matchedValue is not null &&
                                candidate.distance <= FuzzyDistanceLimit(query.Length, candidate.matchedValue.Length))
            .OrderBy(candidate => candidate.distance)
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.BookId, StringComparer.Ordinal)
            .Take(MaxResults)
            .Select(candidate => new MetadataSearchResult(
                candidate.BookId,
                candidate.Title,
                candidate.Author,
                Score: Math.Max(1, 45 - candidate.distance * 5),
                MatchedFields: [candidate.field]))
            .ToList();
    }

    private static int FuzzyDistanceLimit(int queryLength, int candidateLength) =>
        Math.Clamp(Math.Max(queryLength, candidateLength) / 3, 2, 4);

    private static int LevenshteinDistance(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue / 2;
        }

        string target = query.ToUpperInvariant();
        string[] tokens = value
            .Split([' ', '.', ',', ';', ':', '-', '_', '\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToUpperInvariant())
            .ToArray();
        int best = LevenshteinDistanceCore(value.Trim().ToUpperInvariant(), target);
        foreach (string token in tokens)
        {
            best = Math.Min(best, LevenshteinDistanceCore(token, target));
        }

        return best;
    }

    private static int LevenshteinDistanceCore(string candidate, string target)
    {
        int[] previous = Enumerable.Range(0, target.Length + 1).ToArray();
        int[] current = new int[target.Length + 1];

        for (int row = 1; row <= candidate.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= target.Length; column++)
            {
                int substitution = previous[column - 1] + (candidate[row - 1] == target[column - 1] ? 0 : 1);
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }
        return previous[target.Length];
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
