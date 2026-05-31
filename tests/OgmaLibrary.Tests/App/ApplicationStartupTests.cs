using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    [Fact]
    public async Task InitializeAsync_StartsHostedServices_AndStopAsyncStopsThem()
    {
        string dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-hosted-{Guid.NewGuid():N}");

        try
        {
            var hosted = new RecordingHostedService();
            await using ServiceProvider services = new ServiceCollection()
                .AddCatalogueContext(dataDirectory, dataDirectory)
                .AddSingleton<IHostedService>(hosted)
                .BuildServiceProvider();

            await ApplicationStartup.InitializeAsync(services);
            await ApplicationStartup.StopAsync(services);

            Assert.Equal(1, hosted.StartCount);
            Assert.Equal(1, hosted.StopCount);
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

    private sealed class RecordingHostedService : IHostedService
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }
}
