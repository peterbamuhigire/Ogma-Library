using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.App;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
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
            var hosted = new RecordingHostedService();
            await using ServiceProvider services = new ServiceCollection()
                .AddOgmaLibrary(new OgmaRuntimeOptions
                {
                    DataDirectory = dataDirectory,
                    LibraryRoot = dataDirectory,
                })
                .AddSingleton<IHostedService>(hosted)
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });

            ApplicationStartupReport report = await ApplicationStartup.InitializeAsync(services);

            var context = services.GetRequiredService<CatalogueDbContext>();
            Assert.True(report.CanOpenCatalogue);
            Assert.Equal(0, await context.BookFiles.CountAsync());
            Assert.Equal(1, hosted.StartCount);

            await ApplicationStartup.StopAsync(services);
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

    [Fact]
    public void CatalogueContext_ResolvesDistinctInstances_ForForegroundAndWorkerSafety()
    {
        string dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-context-lifetime-{Guid.NewGuid():N}");

        try
        {
            using ServiceProvider services = new ServiceCollection()
                .AddCatalogueContext(dataDirectory, dataDirectory)
                .BuildServiceProvider();

            var first = services.GetRequiredService<CatalogueDbContext>();
            var second = services.GetRequiredService<CatalogueDbContext>();

            Assert.NotSame(first, second);
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
