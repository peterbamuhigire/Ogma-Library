using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 15 OCR/password schema and extracted-page source tests.</summary>
public sealed class Phase15OcrSchemaTests : IDisposable
{
    private const string PreviousMigration = "20260601122941_Phase12AiGatewayTables";
    private const string Phase15Migration = "20260601160606_Phase15OcrPowerReaderSchema";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase15OcrSchemaTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public void Phase15Migration_AddsOcrAndPasswordColumns()
    {
        _context.Database.Migrate();

        Assert.Contains("IsOcrDerived", GetColumns("Books"));
        Assert.Contains("IsPasswordProtected", GetColumns("Books"));
        Assert.Contains("Source", GetColumns("ExtractedPages"));
        Assert.True(IndexExists("IX_ExtractedPages_BookId_Source_PageNumber"));
        Assert.True(IndexExists("IX_Books_IsOcrDerived"));
        Assert.True(IndexExists("IX_Books_IsPasswordProtected"));
    }

    [Fact]
    public void Migration_M015_UpAndDown_LeavesData_Intact()
    {
        IMigrator migrator = _context.Database.GetService<IMigrator>();
        migrator.Migrate(PreviousMigration);
        _context.Books.Add(new BookRow
        {
            BookId = "BOOKPHASE15SCHEMA000001",
            Title = "Phase 15 Schema Book",
            Status = 0,
        });
        _context.SaveChanges();

        migrator.Migrate(Phase15Migration);
        Assert.Contains("IsOcrDerived", GetColumns("Books"));

        migrator.Migrate(PreviousMigration);

        Assert.DoesNotContain("IsOcrDerived", GetColumns("Books"));
        Assert.True(_context.Books.AsNoTracking().Any(book => book.BookId == "BOOKPHASE15SCHEMA000001"));
    }

    [Fact]
    public async Task ExtractedTextStore_AllowsExtractionAndOcrRows_ForSamePage()
    {
        _context.Database.Migrate();
        string bookId = SeedBook();
        var store = new ExtractedTextStore(_context);

        await store.UpsertPageAsync(Page(bookId, "native text", "Extraction"), CancellationToken.None);
        await store.UpsertPageAsync(Page(bookId, "ocr text", "OCR"), CancellationToken.None);

        List<ExtractedPageRow> pages = _context.ExtractedPages
            .Where(page => page.BookId == bookId && page.PageNumber == 0)
            .OrderBy(page => page.Source)
            .ToList();
        Assert.Equal(2, pages.Count);
        Assert.Contains(pages, page => page.Source == "Extraction");
        Assert.Contains(pages, page => page.Source == "OCR");
    }

    private static ExtractedPageRecord Page(string bookId, string text, string source) =>
        new(
            Id: 0,
            BookId: bookId,
            PageIndex: 0,
            Text: text,
            Quality: SearchExtractionQuality.Full,
            WordCount: 2,
            ContentHash: new string('c', 64),
            ExtractedAtUtc: DateTimeOffset.UtcNow,
            Source: source);

    private string SeedBook()
    {
        string bookId = "BOOKPHASE15TEXT0000001";
        _context.Books.Add(new BookRow
        {
            BookId = bookId,
            Title = "Phase 15 Text Book",
            Status = 0,
        });
        _context.SaveChanges();
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

    private bool IndexExists(string indexName)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'index' AND name = $name
            """;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = indexName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }
}
