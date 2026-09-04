using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Regression tests for catalogue projection repair behavior.</summary>
public sealed class CatalogueReadModelTests
{
    [Fact]
    public async Task GetBookSummaries_50kServerSidePage_CompletesWithinTwoSeconds()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            await context.Database.MigrateAsync();
            context.Books.AddRange(Enumerable.Range(0, 50_000).Select(index => new BookRow
            {
                BookId = $"PH19PERF{index:000000}",
                Title = $"Catalogue Performance Book {index:000000}",
                Status = 0,
            }));
            await context.SaveChangesAsync();

            var readModel = new CatalogueReadModel(context);
            var stopwatch = Stopwatch.StartNew();
            var summaries = new List<BookSummaryProjection>();
            await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
                new CatalogueFilter(MaxResults: 100)))
            {
                summaries.Add(summary);
            }

            stopwatch.Stop();
            Assert.Equal(100, summaries.Count);
            Assert.True(
                stopwatch.Elapsed <= TimeSpan.FromSeconds(2),
                $"50k catalogue page took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    [Fact]
    public async Task GetBookSummaries_AppliesOrderedServerSidePage()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.AddRange(
            new BookRow { BookId = "PAGE-BOOK-A", Title = "Alpha", Status = 0 },
            new BookRow { BookId = "PAGE-BOOK-B", Title = "Bravo", Status = 0 },
            new BookRow { BookId = "PAGE-BOOK-C", Title = "Charlie", Status = 0 });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);
        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
            new CatalogueFilter(MaxResults: 1, SkipCount: 1)))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection pageItem = Assert.Single(summaries);
        Assert.Equal("PAGE-BOOK-B", pageItem.BookId);
    }

    [Fact]
    public async Task GetBookSummaries_ProjectsProcessingAndQualityState()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "STATE-BOOK",
            Title = "Processing State",
            Status = 0,
            IndexStatus = (int)OgmaLibrary.Application.Search.SearchBookIndexStatus.Indexed,
            EmbeddingStatus = (int)OgmaLibrary.Application.Search.SearchEmbeddingStatus.Embedded,
            QualityScore = 0.875,
            IsOcrDerived = true,
        });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);
        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection item in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            summaries.Add(item);
        }

        BookSummaryProjection summary = Assert.Single(summaries);

        Assert.NotNull(summary.Processing);
        Assert.Equal(
            OgmaLibrary.Application.Search.SearchBookIndexStatus.Indexed,
            summary.Processing!.IndexStatus);
        Assert.Equal(
            OgmaLibrary.Application.Search.SearchEmbeddingStatus.Embedded,
            summary.Processing.EmbeddingStatus);
        Assert.Equal(0.875, summary.Processing.QualityScore, precision: 3);
        Assert.True(summary.Processing.IsOcrDerived);
        Assert.True(summary.Processing.HasProcessingState);
        Assert.True(summary.Processing.HasQualityScore);
    }

    [Fact]
    public async Task GetBookSummaries_EvaluatesSavedSmartShelfBeforePaging()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.AddRange(
            new BookRow { BookId = "SMART-BOOK-A", Title = "Alpha", Status = 0, Rating = 5, Year = 2020 },
            new BookRow { BookId = "SMART-BOOK-B", Title = "Bravo", Status = 0, Rating = 3, Year = 2020 },
            new BookRow { BookId = "SMART-BOOK-C", Title = "Charlie", Status = 0, Rating = 5, Year = 2019 });
        context.Shelves.Add(new ShelfRow
        {
            ShelfId = "SMART-SHELF",
            Name = "Five Star Books",
            ShelfType = 1,
            Query = JsonSerializer.Serialize(new[]
            {
                new SmartShelfCondition(
                    SmartShelfField.Rating,
                    SmartShelfOperator.Equals,
                    "5"),
            }),
        });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);
        var page = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
            new CatalogueFilter(ShelfId: "SMART-SHELF", MaxResults: 1, SkipCount: 1)))
        {
            page.Add(summary);
        }

        BookSummaryProjection item = Assert.Single(page);
        Assert.Equal("SMART-BOOK-C", item.BookId);

        List<ShelfProjection> shelves = [];
        await foreach (ShelfProjection shelf in readModel.GetShelvesAsync())
        {
            shelves.Add(shelf);
        }

        ShelfProjection smartShelf = Assert.Single(shelves);
        Assert.True(smartShelf.IsSmart);
        Assert.Equal(2, smartShelf.BookCount);
    }

    [Fact]
    public async Task GetBookSummaries_InvalidSavedSmartShelfQueryFailsClosed()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow { BookId = "SMART-DAMAGED-BOOK", Title = "Should not appear", Status = 0 });
        context.Shelves.Add(new ShelfRow
        {
            ShelfId = "SMART-DAMAGED",
            Name = "Damaged Smart Shelf",
            ShelfType = 1,
            Query = "{\"field\":\"Status\",\"operator\":\"Equals\",\"value\":\"0\"}",
        });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);
        var results = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
            new CatalogueFilter(ShelfId: "SMART-DAMAGED")))
        {
            results.Add(summary);
        }

        Assert.Empty(results);
        ShelfProjection shelf = Assert.Single(await CollectShelvesAsync(readModel));
        Assert.Equal(0, shelf.BookCount);
    }

    private static async Task<List<ShelfProjection>> CollectShelvesAsync(CatalogueReadModel readModel)
    {
        var shelves = new List<ShelfProjection>();
        await foreach (ShelfProjection shelf in readModel.GetShelvesAsync())
        {
            shelves.Add(shelf);
        }

        return shelves;
    }

    [Fact]
    public async Task GetBookSummaries_CollapsesReviewedEditionGroupToDeterministicRepresentative()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.AddRange(
            new BookRow { BookId = "GROUPED-BOOK-A", Title = "Grouped title", Status = 0 },
            new BookRow { BookId = "GROUPED-BOOK-B", Title = "Grouped title", Status = 0 });
        await context.SaveChangesAsync();

        var grouping = new StubIdentityGroupingService(
            new IdentityGroupBookMembership("GROUPED-BOOK-A", "GROUPED-EDITION-GROUP", IdentityGroupKind.Edition),
            new IdentityGroupBookMembership("GROUPED-BOOK-B", "GROUPED-EDITION-GROUP", IdentityGroupKind.Edition));
        var readModel = new CatalogueReadModel(context, identityGrouping: grouping);
        var summaries = new List<BookSummaryProjection>();

        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection visible = Assert.Single(summaries);
        Assert.Equal("GROUPED-BOOK-A", visible.BookId);
    }

    [Fact]
    public async Task GetBookSummaries_UsesPrimaryFileName_WhenTitleMetadataIsMissing()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "UNTITLED-PDF",
            Status = 0,
        });
        context.BookFiles.Add(new BookFileRow
        {
            BookId = "UNTITLED-PDF",
            RelativePath = "Ugandan Books, Laws, History/Hackers and Painters.pdf",
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);

        var summaries = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(
            new CatalogueFilter(TitleContains: "Hackers")))
        {
            summaries.Add(summary);
        }

        BookSummaryProjection visible = Assert.Single(summaries);
        Assert.Equal("UNTITLED-PDF", visible.BookId);
        Assert.Equal("Hackers and Painters", visible.Title);
        Assert.True(visible.IsAvailable);
    }

    [Fact]
    public async Task GetBookDetail_UsesPrimaryFileNameAndPath_WhenBookRelativePathIsMissing()
    {
        using CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new BookRow
        {
            BookId = "DETAIL-PDF",
            Status = 0,
        });
        context.BookFiles.Add(new BookFileRow
        {
            BookId = "DETAIL-PDF",
            RelativePath = "C:/Users/Peter/Documents/Interns Speech.pdf",
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var readModel = new CatalogueReadModel(context);

        BookDetailProjection? detail = await readModel.GetBookDetailAsync("DETAIL-PDF");

        Assert.NotNull(detail);
        Assert.Equal("Interns Speech", detail.Title);
        Assert.Equal("C:/Users/Peter/Documents/Interns Speech.pdf", detail.RelativePath);
    }

    [Fact]
    public async Task GetBookSummaries_RepairsMissingBookFilesTableBeforeProjectingAvailability()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-readmodel-repair-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;

            await using (var setup = new CatalogueDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Books.Add(new BookRow
                {
                    BookId = "READMODEL-REPAIR",
                    Title = "Repair Projection",
                    Status = 0,
                });
                setup.BookFiles.Add(new BookFileRow
                {
                    BookId = "READMODEL-REPAIR",
                    RelativePath = "repair-projection.pdf",
                    FileStatus = 0,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                });
                await setup.SaveChangesAsync();

                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
                await setup.Database.ExecuteSqlRawAsync("DROP TABLE BookFiles;");
                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
            }

            SqliteConnection.ClearAllPools();

            await using var context = new CatalogueDbContext(options);
            var readModel = new CatalogueReadModel(context, new CatalogueMigrator(context));

            var summaries = new List<BookSummaryProjection>();
            await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(new CatalogueFilter()))
            {
                summaries.Add(summary);
            }

            BookSummaryProjection repaired = Assert.Single(summaries);
            Assert.Equal("READMODEL-REPAIR", repaired.BookId);
            Assert.False(repaired.IsAvailable);
            Assert.Equal(0, await context.BookFiles.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            foreach (string bak in Directory.GetFiles(
                Path.GetTempPath(),
                Path.GetFileName(dbPath) + "*.bak"))
            {
                File.Delete(bak);
            }
        }
    }

    private sealed class StubIdentityGroupingService(params IdentityGroupBookMembership[] memberships)
        : IIdentityGroupingService
    {
        public Task<IdentityGroupDescriptor> CreateAsync(
            IdentityGroupKind kind,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> MergeAsync(
            string groupId,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> SplitAsync(
            string groupId,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> UndoLastAsync(
            string groupId,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor?> FindByOccurrenceAsync(
            string occurrenceId,
            CancellationToken cancellationToken = default) => Task.FromResult<IdentityGroupDescriptor?>(null);

        public Task<IReadOnlyList<IdentityGroupBookMembership>> FindBookMembershipsAsync(
            IReadOnlyList<string> bookIds,
            bool includeWorkGroups,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentityGroupBookMembership>>(
                memberships
                    .Where(item => bookIds.Contains(item.BookId, StringComparer.Ordinal))
                    .Where(item => includeWorkGroups || item.Kind == IdentityGroupKind.Edition)
                    .ToArray());
    }
}
