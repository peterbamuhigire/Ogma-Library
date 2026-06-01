using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Tests.Reader;

/// <summary>Regression tests for resolving catalogue book files into readable PDF paths.</summary>
public sealed class BookFileLocatorTests
{
    [Fact]
    public async Task BookFileLocator_ProductionDi_RepairsMissingBookFilesTableBeforeQuerying()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"ogma-locator-repair-{Guid.NewGuid():N}");
        string dataDirectory = Path.Combine(tempRoot, "app-data");

        try
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddCatalogueContext(dataDirectory, dataDirectory)
                .AddIngestionPipeline(dataDirectory)
                .AddSingleton<IBookFileLocator, BookFileLocator>()
                .BuildServiceProvider();

            await using (var setup = services.GetRequiredService<CatalogueDbContext>())
            {
                await setup.Database.MigrateAsync();
                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
                await setup.Database.ExecuteSqlRawAsync("DROP TABLE BookFiles;");
                await setup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
            }

            SqliteConnection.ClearAllPools();

            await services.GetRequiredService<ILibrarySettingsService>()
                .SetLibraryRootAsync(tempRoot);

            string? locatedPath = await services.GetRequiredService<IBookFileLocator>()
                .LocateAsync("unknown-book", CancellationToken.None);

            Assert.Null(locatedPath);

            await using var verification = services.GetRequiredService<CatalogueDbContext>();
            Assert.Equal(0, await verification.BookFiles.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
