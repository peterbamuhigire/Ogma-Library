using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Combines metadata and FTS5 hits into a book-level result set.
/// </summary>
public sealed class CombinedSearchService : ICombinedSearchService
{
    private const int ReciprocalRankConstant = 60;
    private readonly IMetadataSearchService _metadataSearch;
    private readonly IFtsIndexService _ftsIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="CombinedSearchService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public CombinedSearchService(
        IMetadataSearchService metadataSearch,
        IFtsIndexService ftsIndex)
    {
        ArgumentNullException.ThrowIfNull(metadataSearch);
        ArgumentNullException.ThrowIfNull(ftsIndex);

        _metadataSearch = metadataSearch;
        _ftsIndex = ftsIndex;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CombinedSearchResult>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IReadOnlyList<MetadataSearchResult> metadata = await _metadataSearch
            .SearchAsync(query, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<FtsSearchResult> fts = await _ftsIndex
            .SearchAsync(query, limit, cancellationToken)
            .ConfigureAwait(false);

        var byBook = new Dictionary<string, MutableCombinedResult>(StringComparer.Ordinal);
        Dictionary<string, int> metadataRanks = RankBooks(
            metadata
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.BookId, StringComparer.Ordinal),
            result => result.BookId);
        Dictionary<string, int> ftsRanks = RankBooks(
            fts
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.BookId, StringComparer.Ordinal)
                .ThenBy(result => result.ChunkIndex),
            result => result.BookId);
        foreach (MetadataSearchResult result in metadata)
        {
            MutableCombinedResult mutable = GetOrAdd(byBook, result.BookId, result.Title, result.Author);
            mutable.Score += ReciprocalRankScore(metadataRanks[result.BookId]);
            mutable.MatchedFields.AddRange(result.MatchedFields);
        }

        foreach (FtsSearchResult hit in fts)
        {
            MutableCombinedResult mutable = GetOrAdd(byBook, hit.BookId, hit.Title, hit.Author);
            mutable.Score += ReciprocalRankScore(ftsRanks[hit.BookId]);
            mutable.FtsHits.Add(hit);
            mutable.MatchedFields.Add("full-text:" + hit.Source.ToString().ToLowerInvariant());
        }

        return byBook.Values
            .Select(result => new CombinedSearchResult(
                result.BookId,
                result.Title,
                result.Author,
                result.Score,
                result.MatchedFields.Distinct(StringComparer.Ordinal).ToList(),
                result.FtsHits
                    .OrderByDescending(hit => hit.Score)
                    .ThenBy(hit => hit.ChunkIndex)
                    .ToList(),
                FusionVersion: "rrf-v1"))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.BookId, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    private static Dictionary<string, int> RankBooks<T>(
        IEnumerable<T> results,
        Func<T, string> bookIdSelector)
    {
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((T result, int index) in results.Select((result, index) => (result, index)))
        {
            ranks.TryAdd(bookIdSelector(result), index + 1);
        }

        return ranks;
    }

    private static double ReciprocalRankScore(int rank) =>
        1.0 / (ReciprocalRankConstant + Math.Max(1, rank));

    private static MutableCombinedResult GetOrAdd(
        Dictionary<string, MutableCombinedResult> byBook,
        string bookId,
        string? title,
        string? author)
    {
        if (!byBook.TryGetValue(bookId, out MutableCombinedResult? result))
        {
            result = new MutableCombinedResult(bookId, title, author);
            byBook.Add(bookId, result);
        }
        else
        {
            result.Title ??= title;
            result.Author ??= author;
        }

        return result;
    }

    private sealed class MutableCombinedResult
    {
        public MutableCombinedResult(string bookId, string? title, string? author)
        {
            BookId = bookId;
            Title = title;
            Author = author;
        }

        public string BookId { get; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public double Score { get; set; }

        public List<string> MatchedFields { get; } = [];

        public List<FtsSearchResult> FtsHits { get; } = [];
    }
}
