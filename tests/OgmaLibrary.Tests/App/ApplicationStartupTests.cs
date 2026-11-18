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

    /// <summary>
    /// Sept-23 stabilisation: on a fresh data folder the processing-progress refresh (scheduled
    /// when the shell is composed) used to query <c>Jobs</c> before the migration created it
    /// (<c>no such table: Jobs</c>). It must wait for the migration instead.
    /// </summary>
    [Fact]
    public async Task ProcessingProgress_BeforeMigration_WaitsInsteadOfQueryingMissingTables()
    {
        string dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-startup-race-{Guid.NewGuid():N}");

        try
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddOgmaLibrary(new OgmaRuntimeOptions
                {
                    DataDirectory = dataDirectory,
                    LibraryRoot = dataDirectory,
                })
                .AddSingleton<IHostedService>(new RecordingHostedService())
                .BuildServiceProvider();
            var readiness = services.GetRequiredService<OgmaLibrary.Application.Catalogue.ICatalogueReadiness>();
            var progress = services.GetRequiredService<OgmaLibrary.Application.Ingestion.IProcessingProgressService>();

            Task<OgmaLibrary.Application.Ingestion.ProcessingSnapshot> early = progress.RefreshAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.False(readiness.IsReady);
            Assert.False(early.IsCompleted, "The progress query ran before the catalogue migration.");

            ApplicationStartupReport report = await ApplicationStartup.InitializeAsync(services);

            Assert.True(report.CanOpenCatalogue);
            Assert.True(readiness.IsReady);
            OgmaLibrary.Application.Ingestion.ProcessingSnapshot snapshot =
                await early.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(snapshot.IsActive);
            await ApplicationStartup.StopAsync(services);
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
