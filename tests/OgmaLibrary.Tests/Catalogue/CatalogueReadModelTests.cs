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
