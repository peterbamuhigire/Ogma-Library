using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 13 (task 13.9): keyword query latency over a 2,000-book synthetic catalogue
/// (10 text pages per book). Budget: p95 ≤ 300 ms locally. Category=Benchmark: excluded from the fast suite (scripts/Test-Performance.ps1).
/// </summary>
public sealed class UnifiedSearchPerformanceTests
{
    private static readonly string[] Vocabulary =
    [
        "river", "kingdom", "harvest", "lantern", "caravan", "monsoon", "algorithm", "savanna",
        "library", "ecology", "trade", "archive", "compass", "orbit", "prism", "canopy",
        "ledger", "voyage", "harbour", "market", "school", "poetry", "sorting", "delta",
    ];

    private readonly ITestOutputHelper _output;

    public UnifiedSearchPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task UnifiedSearch_2000Books_P95Within300ms()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            context.Database.Migrate();
            Seed(context, 2_000, 10);
            var service = new UnifiedSearchService(context, new FtsIndexService(context));
            string[] queries =
            [
                "river", "Lantern Keeper 0042", "caravan monsoon", "author:Author 0500", "algoritm",
                "\"harvest and the\"", "kingdom -river", "Book 1999", "isbn:9780000001234", "zzqxnotaword",
                "trade archive", "savana", "compass orbit prism", "school", "Delta Harbour",
            ];
            foreach (string query in queries)
            {
                _ = await service.SearchAsync(query, 30, CancellationToken.None);
            }

            var elapsed = new List<double>();
            for (int round = 0; round < 4; round++)
            {
                foreach (string query in queries)
                {
                    var clock = Stopwatch.StartNew();
                    _ = await service.SearchAsync(query, 30, CancellationToken.None);
                    elapsed.Add(clock.Elapsed.TotalMilliseconds);
                }
            }

            var fts = new FtsIndexService(context);
            foreach (string query in queries)
            {
                var ftsClock = Stopwatch.StartNew();
                int hits = (await fts.SearchAsync(query, 100, CancellationToken.None)).Count;
                double ftsMs = ftsClock.Elapsed.TotalMilliseconds;
                var loadClock = Stopwatch.StartNew();
                int books = (await SearchBookCandidateLoader.LoadAsync(context, CancellationToken.None)).Count;
                _output.WriteLine($"component '{query}': fts {ftsMs:F1} ms ({hits} hits), candidates {loadClock.Elapsed.TotalMilliseconds:F1} ms ({books})");
            }

            elapsed.Sort();
            double p50 = elapsed[(elapsed.Count / 2) - 1];
            double p95 = elapsed[(int)Math.Ceiling(elapsed.Count * 0.95) - 1];
            _output.WriteLine($"unified search 2000 books: n={elapsed.Count} p50={p50:F1} ms p95={p95:F1} ms max={elapsed[^1]:F1} ms");
            Assert.True(p95 <= 300, $"p95 {p95:F1} ms > 300 ms (p50 {p50:F1} ms)");
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    private static void Seed(CatalogueDbContext context, int bookCount, int pagesPerBook)
    {
        var random = new Random(13);
        DateTimeOffset now = new(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        for (int index = 0; index < bookCount; index++)
        {
            string bookId = $"01PERF{index:00000000000000000000}";
            string title = $"{Word(random)} {Word(random)} Book {index:0000}";
            context.Books.Add(new BookRow
            {
                BookId = bookId,
                RelativePath = $"Shelf{index % 20}/{title}.pdf",
                IsbnNormalized = $"978{index:0000000000}",
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                TextStatus = (int)BookTextStatus.Searchable,
            });
            context.BookFiles.Add(new BookFileRow { BookId = bookId, RelativePath = $"Shelf{index % 20}/{title}.pdf", LastSeenUtc = now });
            context.BookMetadataFields.Add(new BookMetadataFieldRow { BookId = bookId, FieldName = "Title", Value = title, Source = "PDF", Confidence = 0.5 });
            context.BookMetadataFields.Add(new BookMetadataFieldRow { BookId = bookId, FieldName = "Author", Value = $"Author {index % 700:0000}", Source = "PDF", Confidence = 0.5 });
            for (int page = 0; page < pagesPerBook; page++)
            {
                string text = string.Join(' ', Enumerable.Range(0, 60).Select(_ => Word(random)));
                context.SearchChunks.Add(new SearchChunkRow
                {
                    BookId = bookId,
                    ChunkIndex = page,
                    ChunkText = text,
                    Source = (int)SearchChunkSource.Page,
                    TokenCount = 60,
                    CreatedAtUtc = now,
                });
            }

            if (index % 250 == 249)
            {
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        }

        context.SaveChanges();
        context.ChangeTracker.Clear();
    }

    private static string Word(Random random) => Vocabulary[random.Next(Vocabulary.Length)];
}
