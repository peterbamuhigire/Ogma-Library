using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 WP1 schema, repository, and FTS5 trigger tests.
/// </summary>
public sealed class Phase10SearchIndexSchemaTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase10SearchIndexSchemaTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public void Phase10Migration_AddsSearchIndexColumnsAndFts5Objects()
    {
        Assert.Contains("IndexStatus", GetColumns("Books"));
        Assert.Contains("ExtractionQuality", GetColumns("ExtractedPages"));
        Assert.Contains("WordCount", GetColumns("ExtractedPages"));
        Assert.Contains("ContentHash", GetColumns("ExtractedPages"));
        Assert.Contains("Source", GetColumns("SearchChunks"));
        Assert.Contains("CreatedAtUtc", GetColumns("SearchChunks"));

        Assert.True(ObjectExists("table", "SearchFts5"));
        Assert.True(ObjectExists("trigger", "SearchChunks_Fts_Insert"));
        Assert.True(ObjectExists("trigger", "SearchChunks_Fts_Update"));
        Assert.True(ObjectExists("trigger", "SearchChunks_Fts_Delete"));
    }

    [Fact]
    public void Fts5Triggers_InsertUpdateDelete_SearchChunkKeepIndexConsistent()
    {
        string bookId = SeedBook();
        var chunk = new SearchChunkRow
        {
            BookId = bookId,
            ChunkIndex = 0,
            ChunkText = "introductory chapter with a rarephase10token",
            Source = (int)SearchChunkSource.Page,
            TokenCount = 5,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _context.SearchChunks.Add(chunk);
        _context.SaveChanges();

        Assert.Equal(1, CountFtsMatches("rarephase10token"));

        chunk.ChunkText = "updated chapter with replacementphase10token";
        _context.SaveChanges();

        Assert.Equal(0, CountFtsMatches("rarephase10token"));
        Assert.Equal(1, CountFtsMatches("replacementphase10token"));

        _context.SearchChunks.Remove(chunk);
        _context.SaveChanges();

        Assert.Equal(0, CountFtsMatches("replacementphase10token"));
        _context.Database.ExecuteSqlRaw("INSERT INTO SearchFts5(SearchFts5) VALUES ('integrity-check');");
    }

    [Fact]
    public async Task ExtractedTextStore_UpsertPage_RoundTripsAndKeepsOneRow()
    {
        string bookId = SeedBook();
        var store = new ExtractedTextStore(_context);
        var first = new ExtractedPageRecord(
            Id: 0,
            BookId: bookId,
            PageIndex: 2,
            Text: "first extracted text",
            Quality: SearchExtractionQuality.Partial,
            WordCount: 3,
            ContentHash: new string('a', 64),
            ExtractedAtUtc: DateTimeOffset.UtcNow);

        ExtractedPageRecord saved = await store.UpsertPageAsync(first, CancellationToken.None);
        ExtractedPageRecord second = saved with
        {
            Text = "complete extracted text for indexing",
            Quality = SearchExtractionQuality.Full,
            WordCount = 5,
            ContentHash = new string('b', 64),
            ExtractedAtUtc = saved.ExtractedAtUtc.AddMinutes(1),
        };

        ExtractedPageRecord updated = await store.UpsertPageAsync(second, CancellationToken.None);
        ExtractedPageRecord? reloaded = await store.GetPageAsync(bookId, 2, CancellationToken.None);

        Assert.Equal(saved.Id, updated.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(SearchExtractionQuality.Full, reloaded.Quality);
        Assert.Equal(new string('b', 64), reloaded.ContentHash);
        Assert.Equal(1, _context.ExtractedPages.Count(p => p.BookId == bookId && p.PageNumber == 2));
    }

    [Fact]
    public async Task SearchChunkRepository_ReplaceForBook_IsSourceScopedAndFtsBacked()
    {
        string bookId = SeedBook();
        var repository = new SearchChunkRepository(_context);

        await repository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Note,
            [
                NewChunk(bookId, SearchChunkSource.Note, 0, "phase10 note marker"),
            ],
            CancellationToken.None);

        await repository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Page,
            [
                NewChunk(bookId, SearchChunkSource.Page, 0, "old page marker"),
                NewChunk(bookId, SearchChunkSource.Page, 1, "second old page marker"),
            ],
            CancellationToken.None);

        await repository.ReplaceForBookAsync(
            bookId,
            SearchChunkSource.Page,
            [
                NewChunk(bookId, SearchChunkSource.Page, 0, "new page replacement marker"),
            ],
            CancellationToken.None);

        IReadOnlyList<SearchChunkRecord> chunks = await repository.ListForBookAsync(bookId, CancellationToken.None);

        Assert.Equal(2, chunks.Count);
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Note && c.Text.Contains("note", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Source == SearchChunkSource.Page && c.Text.Contains("replacement", StringComparison.Ordinal));
        Assert.Equal(0, CountFtsMatches("old"));
        Assert.Equal(1, CountFtsMatches("replacement"));
        Assert.Equal(1, CountFtsMatches("note"));
    }

    private static SearchChunkRecord NewChunk(
        string bookId,
        SearchChunkSource source,
        int chunkIndex,
        string text) =>
        new(
            Id: 0,
            BookId: bookId,
            ExtractedPageId: null,
            PageIndex: null,
            ChunkIndex: chunkIndex,
            Text: text,
            TokenCount: text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            Source: source,
            CreatedAtUtc: DateTimeOffset.UtcNow);

    private string SeedBook()
    {
        string bookId = "BOOKPHASE10SCHEMA000001";
        if (!_context.Books.Any(b => b.BookId == bookId))
        {
            _context.Books.Add(new BookRow
            {
                BookId = bookId,
                Title = "Phase 10 Schema Book",
            });
            _context.SaveChanges();
        }

        return bookId;
    }

    private HashSet<string> GetColumns(string tableName)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using DbDataReader reader = command.ExecuteReader();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private bool ObjectExists(string objectType, string objectName)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = $type AND name = $name
            """;
        AddParameter(command, "$type", objectType);
        AddParameter(command, "$name", objectName);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private int CountFtsMatches(string query)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM SearchFts5 WHERE SearchFts5 MATCH $query;";
        AddParameter(command, "$query", query);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
