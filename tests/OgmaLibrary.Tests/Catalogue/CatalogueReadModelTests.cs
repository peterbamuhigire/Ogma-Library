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
}
