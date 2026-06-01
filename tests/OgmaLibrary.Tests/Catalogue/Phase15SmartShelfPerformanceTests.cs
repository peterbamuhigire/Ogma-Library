using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 15 smart-shelf query plan and performance tests.</summary>
public sealed class Phase15SmartShelfPerformanceTests : IDisposable
{
    private const int CorpusSize = 2_000;
    private const double P95BudgetMilliseconds = 2_000;
    private const string ShelfId = "SHELFSMART000000000001";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly ITestOutputHelper _output;

    public Phase15SmartShelfPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public void Phase15SmartShelfMigration_AddsCompositeIndexes()
    {
        Assert.True(IndexExists("IX_Books_Status_Year"));
        Assert.True(IndexExists("IX_ShelfBooks_ShelfId_BookId"));
        Assert.True(IndexExists("IX_BookMetadataFields_FieldName_Value"));
    }

    [Fact]
    public void SmartShelf_QueryPlans_UsePhase15Indexes()
    {
        SeedSmartShelfCorpus();

        Dictionary<string, string> plans = new(StringComparer.Ordinal)
        {
            ["status-year"] = Explain("""
                SELECT COUNT(1)
                FROM Books
                WHERE Status = 0 AND Year >= 2010;
                """),
            ["shelf-rating"] = Explain("""
                SELECT COUNT(1)
                FROM ShelfBooks AS sb
                INNER JOIN Books AS b ON b.BookId = sb.BookId
                WHERE sb.ShelfId = $shelfId AND b.Rating >= 4;
                """,
                ("$shelfId", ShelfId)),
            ["metadata-category"] = Explain("""
                SELECT COUNT(1)
                FROM BookMetadataFields
                WHERE FieldName = 'Category' AND Value = 'Science';
                """),
            ["metadata-tag-active"] = Explain("""
                SELECT COUNT(DISTINCT b.BookId)
                FROM BookMetadataFields AS f
                INNER JOIN Books AS b ON b.BookId = f.BookId
                WHERE f.FieldName = 'Tag' AND f.Value = 'Reference' AND b.Status = 0;
                """),
            ["status-year-category"] = Explain("""
                SELECT COUNT(DISTINCT b.BookId)
                FROM BookMetadataFields AS f
                INNER JOIN Books AS b ON b.BookId = f.BookId
                WHERE b.Status = 0
                  AND b.Year >= 2000
                  AND f.FieldName = 'Category'
                  AND f.Value = 'Science';
                """),
        };

        foreach (KeyValuePair<string, string> plan in plans)
        {
            _output.WriteLine($"{plan.Key}: {plan.Value}");
        }

        Assert.Contains("IX_Books_Status_Year", plans["status-year"], StringComparison.Ordinal);
        Assert.Contains("IX_ShelfBooks_ShelfId_BookId", plans["shelf-rating"], StringComparison.Ordinal);
        Assert.Contains("IX_BookMetadataFields_FieldName_Value", plans["metadata-category"], StringComparison.Ordinal);
        Assert.Contains("IX_BookMetadataFields_FieldName_Value", plans["metadata-tag-active"], StringComparison.Ordinal);
        Assert.Contains("IX_BookMetadataFields_FieldName_Value", plans["status-year-category"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmartShelf_QueryBenchmark_2000Books()
    {
        SeedSmartShelfCorpus();

        IReadOnlyList<QueryBenchmark> benchmarks =
        [
            new(
                "active-books-since-2010",
                () => _context.Books
                    .AsNoTracking()
                    .Where(book => book.Status == 0 && book.Year >= 2010)
                    .CountAsync()),
            new(
                "active-high-rating",
                () => _context.Books
                    .AsNoTracking()
                    .Where(book => book.Status == 0 && book.Rating >= 4)
                    .CountAsync()),
            new(
                "shelf-members-high-rating",
                () => _context.Books
                    .AsNoTracking()
                    .Where(book => book.Rating >= 4 && book.ShelfBooks.Any(shelf => shelf.ShelfId == ShelfId))
                    .CountAsync()),
            new(
                "science-category",
                () => _context.Books
                    .AsNoTracking()
                    .Where(book => book.MetadataFields.Any(field =>
                        field.FieldName == "Category" && field.Value == "Science"))
                    .CountAsync()),
            new(
                "active-science-since-2000",
                () => _context.Books
                    .AsNoTracking()
                    .Where(book =>
                        book.Status == 0
                        && book.Year >= 2000
                        && book.MetadataFields.Any(field =>
                            field.FieldName == "Category" && field.Value == "Science"))
                    .CountAsync()),
        ];

        foreach (QueryBenchmark benchmark in benchmarks)
        {
            await benchmark.Query();
        }

        var results = new List<(string Name, double P95Milliseconds, int LastCount)>();
        foreach (QueryBenchmark benchmark in benchmarks)
        {
            List<double> samples = [];
            int lastCount = 0;
            for (int i = 0; i < 10; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                lastCount = await benchmark.Query();
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }

            double p95 = Percentile(samples, 0.95);
            results.Add((benchmark.Name, p95, lastCount));
            Assert.True(
                p95 <= P95BudgetMilliseconds,
                $"{benchmark.Name} P95 was {p95:F2} ms, above {P95BudgetMilliseconds:F0} ms.");
        }

        foreach ((string name, double p95Milliseconds, int lastCount) in results)
        {
            _output.WriteLine($"{name}: p95Ms={p95Milliseconds:F2}, count={lastCount}");
        }
    }

    private void SeedSmartShelfCorpus()
    {
        if (_context.Books.Any())
        {
            return;
        }

        var shelf = new ShelfRow
        {
            ShelfId = ShelfId,
            Name = "Phase 15 Smart Shelf Benchmark",
            ShelfType = 1,
            Query = """{"status":0,"yearMin":2010,"category":"Science"}""",
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        _context.Shelves.Add(shelf);

        for (int i = 0; i < CorpusSize; i++)
        {
            string bookId = $"SSB{i:00000000000000000000000}";
            var book = new BookRow
            {
                BookId = bookId,
                Title = $"Smart Shelf Benchmark Book {i:0000}",
                RelativePath = $"benchmarks/smart-shelf/book-{i:0000}.pdf",
                Status = i % 10 == 0 ? 1 : 0,
                Year = 1985 + (i % 41),
                Rating = 1 + (i % 5),
                IndexStatus = 2,
                MetadataFields =
                [
                    new()
                    {
                        FieldName = "Category",
                        Value = i % 4 == 0 ? "Science" : i % 4 == 1 ? "History" : i % 4 == 2 ? "Literature" : "Math",
                        Source = "Benchmark",
                        Confidence = 0.9,
                        SourceTimestamp = DateTimeOffset.UtcNow,
                    },
                    new()
                    {
                        FieldName = "Tag",
                        Value = i % 3 == 0 ? "Reference" : "Leisure",
                        Source = "Benchmark",
                        Confidence = 0.8,
                        SourceTimestamp = DateTimeOffset.UtcNow,
                    },
                    new()
                    {
                        FieldName = "Language",
                        Value = i % 5 == 0 ? "fr" : "en",
                        Source = "Benchmark",
                        Confidence = 1.0,
                        SourceTimestamp = DateTimeOffset.UtcNow,
                    },
                ],
            };

            if (i % 2 == 0)
            {
                book.ShelfBooks.Add(new ShelfBookRow
                {
                    ShelfId = ShelfId,
                    AddedUtc = DateTimeOffset.UtcNow,
                    DisplayOrder = i / 2,
                });
            }

            _context.Books.Add(book);
        }

        _context.SaveChanges();
        _context.ChangeTracker.Clear();
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
        AddParameter(command, "$name", indexName);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private string Explain(string sql, params (string Name, object Value)[] parameters)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        foreach ((string name, object value) in parameters)
        {
            AddParameter(command, name, value);
        }

        using DbDataReader reader = command.ExecuteReader();
        var details = new List<string>();
        while (reader.Read())
        {
            details.Add(reader.GetString(3));
        }

        return string.Join(" | ", details);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double[] sorted = [.. values.Order()];
        int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private sealed record QueryBenchmark(string Name, Func<Task<int>> Query);
}
