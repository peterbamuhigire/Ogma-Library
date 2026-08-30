using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.AI.Ollama;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Infrastructure.Sidecar;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Extension methods to register the Catalogue bounded-context services with the
/// DI container (Phase 04 deliverable 8 — wire into CompositionRoot).
/// </summary>
public static class CatalogueServiceExtensions
{
    /// <summary>
    /// Registers the catalogue bounded context: <see cref="CatalogueDbContext"/>,
    /// <see cref="CatalogueMigrator"/>, all repository implementations, the sidecar
    /// service, the identity service, and the read model.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="dataDirectory">
    /// The directory under which the <c>catalogue.db</c> file is stored.
    /// Typically the "Ogma Library Data" folder under the OS app-data root.
    /// </param>
    /// <param name="libraryRoot">
    /// The library root folder path used by the sidecar service.
    /// Pass an empty string to use the data directory as the library root.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCatalogueContext(
        this IServiceCollection services,
        string dataDirectory,
        string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        // Ensure the data directory exists.
        Directory.CreateDirectory(dataDirectory);

        string dbPath = Path.Combine(dataDirectory, "catalogue.db");

        services.AddDbContextFactory<CatalogueDbContext>(options =>
        {
            options.UseSqlite(
                $"Data Source={dbPath}",
                sqlite =>
                {
                    sqlite.MigrationsAssembly(typeof(CatalogueDbContext).Assembly.GetName().Name);
                });
        });

        services.AddTransient(sp =>
            sp.GetRequiredService<IDbContextFactory<CatalogueDbContext>>().CreateDbContext());

        // Migrator — runs once at startup via explicit call.
        services.AddSingleton<CatalogueMigrator>();

        // Repository implementations.
        services.AddSingleton<ILegacyCatalogueRepository, LegacyCatalogueRepository>();
        services.AddSingleton<ICanonicalIdentityRepository, CanonicalIdentityRepository>();
        services.AddSingleton<IIdentityDecisionService, IdentityDecisionRepository>();
        services.AddSingleton<IExtractionArtifactService, ExtractionArtifactService>();
        services.AddSingleton<IShelfRepository, ShelfRepository>();
        services.AddSingleton<IAnnotationRepository, AnnotationRepository>();
        services.AddSingleton<IReadingProgressRepository, ReadingProgressRepository>();
        services.AddSingleton<IAuditRepository, AuditRepository>();
        services.AddSingleton<IAiConsentRepository, AiConsentRepository>();
        services.AddSingleton<IAiAuditRepository, AiAuditRepository>();
        services.AddSingleton<IAiQueryHistoryRepository, AiQueryHistoryRepository>();

        // Phase 09 — Annotations, Bookmarks, Layers, Reading Memory.
        services.AddSingleton<IBookmarkRepository, BookmarkRepository>();
        services.AddSingleton<IAnnotationV2Repository, AnnotationV2Repository>();
        services.AddSingleton<IAnnotationLayerRepository, AnnotationLayerRepository>();
        services.AddSingleton<IReadingMemoryRepository, ReadingMemoryRepository>();

        // Phase 10 — Search & indexing.
        services.AddSingleton<SearchChunker>();
        services.AddSingleton<IExtractedTextStore, ExtractedTextStore>();
        services.AddSingleton<ISearchChunkRepository, SearchChunkRepository>();
        services.AddSingleton<IEmbeddingVectorRepository, EmbeddingVectorRepository>();
        services.AddSingleton<IMetadataSearchService, MetadataSearchService>();
        services.AddSingleton<IExtractionPipelineService, ExtractionPipelineService>();
        services.AddSingleton<EmbeddingGenerationService>();
        services.AddSingleton<IEmbeddingGenerationService>(sp =>
            sp.GetRequiredService<EmbeddingGenerationService>());
        services.AddSingleton<ISemanticSearchReadModel>(sp =>
            sp.GetRequiredService<EmbeddingGenerationService>());
        services.AddSingleton<IFtsIndexService, FtsIndexService>();
        services.AddSingleton<ICombinedSearchService, CombinedSearchService>();
        services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
        services.AddSingleton<IHybridRankingService, HybridRankingService>();
        services.AddSingleton<IMatchLocationService, MatchLocationService>();
        services.AddSingleton<IEmbeddingErasureService, EmbeddingErasureService>();
        services.AddSingleton<IOcrJobQueueService>(sp => new OcrJobQueueService(
            sp.GetRequiredService<IDbContextFactory<CatalogueDbContext>>(),
            libraryRoot));
        services.AddSingleton<IndexManagerService>();
        services.AddSingleton<IIndexManagerService>(sp => sp.GetRequiredService<IndexManagerService>());
        services.AddSingleton<ISearchReadModel>(sp => sp.GetRequiredService<IndexManagerService>());
        services.AddHttpClient<IOllamaEmbeddingProvider, OllamaEmbeddingAdapter>();

        // Sidecar service.
        services.AddSingleton<ISidecarService>(_ => new SidecarService(libraryRoot));

        // Identity service.
        services.AddSingleton<IBookIdentityService, BookIdentityService>();

        // Read model.
        services.AddSingleton<CatalogueReadModel>();
        services.AddSingleton<ICatalogueReadModel>(sp => sp.GetRequiredService<CatalogueReadModel>());

        return services;
    }

    /// <summary>
    /// Returns the default "Ogma Library Data" directory under the OS app-data root,
    /// suitable for cross-platform use on Windows and macOS.
    /// </summary>
    /// <returns>An absolute path to the per-user data directory.</returns>
    public static string GetDefaultDataDirectory()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable("OGMA_LIBRARY_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory);
        }

        if (IsRunningUnderTestHost())
        {
            return Path.Combine(
                Path.GetTempPath(),
                "Ogma Library Test Data",
                Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // Environment.SpecialFolder.LocalApplicationData is:
        //   Windows: %LOCALAPPDATA%  (e.g. C:\Users\Name\AppData\Local)
        //   macOS:   ~/Library/Application Support
        //   Linux:   ~/.local/share
        string appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(appData, "Ogma Library Data");
    }

    private static bool IsRunningUnderTestHost()
    {
        string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        return processName.Equals("testhost", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("vstest.console", StringComparison.OrdinalIgnoreCase);
    }
}
