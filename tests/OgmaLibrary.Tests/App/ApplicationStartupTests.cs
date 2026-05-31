using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Tests.App;

/// <summary>Regression tests for desktop startup initialization.</summary>
public sealed class ApplicationStartupTests
{
    [Fact]
    public async Task InitializeAsync_AppliesCatalogueMigrations_BeforeShellQueries()
    {
        string dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-startup-{Guid.NewGuid():N}");

        try
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddCatalogueContext(dataDirectory, dataDirectory)
                .BuildServiceProvider();

            await ApplicationStartup.InitializeAsync(services);

            var context = services.GetRequiredService<CatalogueDbContext>();
            Assert.Equal(0, await context.BookFiles.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }
}
