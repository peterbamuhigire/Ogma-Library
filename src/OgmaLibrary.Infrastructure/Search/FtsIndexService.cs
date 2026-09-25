using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// SQLite FTS5-backed full-text search service for Phase 10.
/// </summary>
public sealed class FtsIndexService : IFtsIndexService
{
    private const int MaxLimit = 100;
    private const int CompletedArtifactStatus = 1;
    private const string CurrentIndexVersion = "fts5-v1";
    private const int MaxHitsPerBook = 3;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="FtsIndexService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public FtsIndexService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FtsIndexService"/> for tests that
    /// share one context.
    /// </summary>
    internal FtsIndexService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FtsSearchResult>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // Sept-23 Phase 13 (K40): all bare words must match in any order (not as one
        // adjacent phrase); quoted input stays a phrase and exclusions become NOT.
        ParsedFtsQuery parsed = ParseQuery(query);
        string matchQuery = FtsMatchQueryBuilder.Build(parsed.Query, parsed.SourceTerms);
        if (matchQuery.Length == 0)
        {
            return [];
        }

        int effectiveLimit = Math.Min(limit, MaxLimit);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        List<long> selected = await SelectChunksAsync(
                connection,
                matchQuery,
                parsed.Source,
                effectiveLimit,
                cancellationToken)
            .ConfigureAwait(false);
        if (selected.Count == 0)
        {
            return [];
        }

        // Snippets and display fields only for the selected chunks.
        using DbCommand command = connection.CreateCommand();
        string idParameters = string.Join(
            ", ",
            selected.Select((_, index) => "$id" + index.ToString(CultureInfo.InvariantCulture)));
        command.CommandText = """
            SELECT
                c.BookId,
                b.Title,
                (
                    SELECT a.NormalizedName
                    FROM BookAuthors ba
                    INNER JOIN Authors a ON a.AuthorId = ba.AuthorId
                    WHERE ba.BookId = c.BookId
                    ORDER BY ba.DisplayOrder, a.NormalizedName
                    LIMIT 1
                ) AS Author,
                c.ChunkId,
                ep.PageNumber,
                c.ChunkIndex,
                c.Source,
                snippet(SearchFts5, 0, '<b>', '</b>', '...', 20) AS Snippet,
                bm25(SearchFts5) AS Rank,
                CASE WHEN ep.Source = 'OCR' THEN 1 ELSE 0 END AS IsOcrText
            FROM SearchFts5
            INNER JOIN SearchChunks c ON c.ChunkId = SearchFts5.rowid
            INNER JOIN Books b ON b.BookId = c.BookId
            LEFT JOIN ExtractedPages ep ON ep.ExtractedPageId = c.ExtractedPageId
            WHERE SearchFts5 MATCH $query
              AND SearchFts5.rowid IN (
            """ + idParameters + """
            )
            ORDER BY Rank, c.BookId, c.ChunkIndex;
            """;
        AddParameter(command, "$query", matchQuery);
        for (int index = 0; index < selected.Count; index++)
        {
            AddParameter(command, "$id" + index.ToString(CultureInfo.InvariantCulture), selected[index]);
        }

        var results = new List<FtsSearchResult>();
        using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            double rank = reader.GetDouble(8);
            string markedSnippet = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            SearchSnippet highlightedSnippet = SearchSnippetParser.Parse(markedSnippet);
            long chunkId = reader.GetInt64(3);
            int? pageIndex = reader.IsDBNull(4) ? null : reader.GetInt32(4);
            SearchChunkSource source = (SearchChunkSource)reader.GetInt32(6);
            string bookId = reader.GetString(0);
            results.Add(new FtsSearchResult(
                BookId: bookId,
                Title: reader.IsDBNull(1) ? null : reader.GetString(1),
                Author: reader.IsDBNull(2) ? null : reader.GetString(2),
                ChunkId: chunkId,
                PageIndex: pageIndex,
                ChunkIndex: reader.GetInt32(5),
                Source: source,
                Snippet: highlightedSnippet.Text,
                Score: -rank,
                HighlightedSnippet: highlightedSnippet,
                PageJumpTarget: source == SearchChunkSource.Page && pageIndex is int page
                    ? new SearchPageJumpTarget(bookId, chunkId, page)
                    : null,
                IsOcrText: reader.GetInt32(9) == 1));
        }

        return results;
    }

    /// <summary>
    /// Ranks matching chunks and keeps at most <see cref="MaxHitsPerBook"/> per book, so one
    /// long book cannot crowd every other book out of the result window.
    /// </summary>
    private static async Task<List<long>> SelectChunksAsync(
        DbConnection connection,
        string matchQuery,
        SearchChunkSource? source,
        int limit,
        CancellationToken cancellationToken)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT ChunkId
            FROM (
                SELECT
                    ChunkId,
                    BookId,
                    ChunkIndex,
                    Rank,
                    ROW_NUMBER() OVER (PARTITION BY BookId ORDER BY Rank, ChunkIndex) AS BookHitRank
                FROM (
                    SELECT c.ChunkId, c.BookId, c.ChunkIndex, bm25(SearchFts5) AS Rank
                    FROM SearchFts5
                    INNER JOIN SearchChunks c ON c.ChunkId = SearchFts5.rowid
                    INNER JOIN Books b ON b.BookId = c.BookId
                    LEFT JOIN ExtractionArtifacts ea ON ea.ExtractionArtifactId = c.ExtractionArtifactId
                    WHERE SearchFts5 MATCH $query
                      AND b.Status = 0
                      AND c.IndexVersion = $indexVersion
                      AND ($source IS NULL OR c.Source = $source)
                      AND (
                          c.ExtractionArtifactId IS NULL
                          OR (
                              ea.Status = $completedArtifactStatus
                              AND (ea.ContentHash IS NULL OR b.Sha256Hash IS NULL OR ea.ContentHash = b.Sha256Hash)
                          )
                      )
                )
            )
            WHERE BookHitRank <= $perBook
            ORDER BY Rank, BookId, ChunkIndex
            LIMIT $limit;
            """;
        AddParameter(command, "$query", matchQuery);
        AddParameter(command, "$limit", limit);
        AddParameter(command, "$perBook", MaxHitsPerBook);
        AddParameter(command, "$indexVersion", CurrentIndexVersion);
        AddParameter(command, "$completedArtifactStatus", CompletedArtifactStatus);
        AddParameter(command, "$source", source is null ? DBNull.Value : (int)source.Value);

        var selected = new List<long>(limit);
        using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            selected.Add(reader.GetInt64(0));
        }

        return selected;
    }

    /// <inheritdoc />
    public async Task<FtsIntegrityResult> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        try
        {
            await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO SearchFts5(SearchFts5) VALUES ('integrity-check');",
                    cancellationToken)
                .ConfigureAwait(false);
            return new FtsIntegrityResult(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FtsIntegrityResult(false, "Full-text index integrity check failed.");
        }
    }

    /// <inheritdoc />
    public async Task<FtsCleanupResult> CleanupStaleAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        try
        {
            int removed = await context.Database.ExecuteSqlRawAsync(
                    """
                    DELETE FROM SearchChunks
                    WHERE BookId NOT IN (
                        SELECT BookId FROM Books WHERE Status = 0
                    )
                    OR (
                        ExtractedPageId IS NOT NULL
                        AND ExtractedPageId NOT IN (
                            SELECT ExtractedPageId FROM ExtractedPages
                        )
                    )
                    OR (
                        ExtractionArtifactId IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM ExtractionArtifacts ea
                            INNER JOIN Books b ON b.BookId = SearchChunks.BookId
                            WHERE ea.ExtractionArtifactId = SearchChunks.ExtractionArtifactId
                              AND ea.Status = 1
                              AND (ea.ContentHash IS NULL OR b.Sha256Hash IS NULL OR ea.ContentHash = b.Sha256Hash)
                        )
                    )
                    OR IndexVersion <> 'fts5-v1';
                    """,
                    cancellationToken)
                .ConfigureAwait(false);
            FtsIntegrityResult integrity = await CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
            return new FtsCleanupResult(removed, integrity.IsHealthy, integrity.ErrorMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FtsCleanupResult(0, false, "Full-text index cleanup failed.");
        }
    }

    private static ParsedFtsQuery ParseQuery(string? query)
    {
        ParsedSearchQuery parsed = SearchQueryParser.Parse(query);
        SearchFieldFilter? sourceFilter = parsed.Filters.FirstOrDefault(filter =>
            !filter.Exclude && ToChunkSource(filter.Field) is not null);
        return sourceFilter is null
            ? new ParsedFtsQuery(parsed, null, [])
            : new ParsedFtsQuery(parsed, ToChunkSource(sourceFilter.Field), [sourceFilter.Value]);
    }

    private static SearchChunkSource? ToChunkSource(SearchField field) => field switch
    {
        SearchField.Text => SearchChunkSource.Page,
        SearchField.Note => SearchChunkSource.Note,
        SearchField.Tag => SearchChunkSource.Tag,
        SearchField.Description => SearchChunkSource.Description,
        SearchField.Toc => SearchChunkSource.Toc,
        _ => null,
    };

    private sealed record ParsedFtsQuery(
        ParsedSearchQuery Query,
        SearchChunkSource? Source,
        IReadOnlyList<string> SourceTerms);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
