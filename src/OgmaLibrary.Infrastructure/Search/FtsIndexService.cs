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

        string matchQuery = BuildMatchQuery(query);
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

        using DbCommand command = connection.CreateCommand();
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
                bm25(SearchFts5) AS Rank
            FROM SearchFts5
            INNER JOIN SearchChunks c ON c.ChunkId = SearchFts5.rowid
            INNER JOIN Books b ON b.BookId = c.BookId
            LEFT JOIN ExtractedPages ep ON ep.ExtractedPageId = c.ExtractedPageId
            LEFT JOIN ExtractionArtifacts ea ON ea.ExtractionArtifactId = c.ExtractionArtifactId
            WHERE SearchFts5 MATCH $query
              AND b.Status = 0
              AND c.IndexVersion = $indexVersion
              AND (
                  c.ExtractionArtifactId IS NULL
                  OR (
                      ea.Status = $completedArtifactStatus
                      AND (ea.ContentHash IS NULL OR b.Sha256Hash IS NULL OR ea.ContentHash = b.Sha256Hash)
                  )
              )
            ORDER BY Rank, c.BookId, c.ChunkIndex
            LIMIT $limit;
            """;
        AddParameter(command, "$query", matchQuery);
        AddParameter(command, "$limit", effectiveLimit);
        AddParameter(command, "$indexVersion", CurrentIndexVersion);
        AddParameter(command, "$completedArtifactStatus", CompletedArtifactStatus);

        var results = new List<FtsSearchResult>();
        using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            double rank = reader.GetDouble(8);
            results.Add(new FtsSearchResult(
                BookId: reader.GetString(0),
                Title: reader.IsDBNull(1) ? null : reader.GetString(1),
                Author: reader.IsDBNull(2) ? null : reader.GetString(2),
                ChunkId: reader.GetInt64(3),
                PageIndex: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                ChunkIndex: reader.GetInt32(5),
                Source: (SearchChunkSource)reader.GetInt32(6),
                Snippet: reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Score: -rank));
        }

        return results;
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
            return new FtsIntegrityResult(false, ex.Message);
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
            return new FtsCleanupResult(0, false, ex.Message);
        }
    }

    private static string BuildMatchQuery(string? query)
    {
        string[] tokens = Tokenize(query).ToArray();
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        return tokens.Length == 1
            ? tokens[0] + "*"
            : "\"" + string.Join(' ', tokens) + "\"";
    }

    private static IEnumerable<string> Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        var token = new System.Text.StringBuilder();
        foreach (char ch in query.Normalize())
        {
            if (char.IsLetterOrDigit(ch))
            {
                token.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
            }
            else if (token.Length > 0)
            {
                yield return token.ToString();
                token.Clear();
            }
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }

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
