using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata.Providers;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Extension methods to register the Metadata Enrichment bounded-context services
/// with the DI container (Phase 07).
/// </summary>
public static class MetadataServiceExtensions
{
    /// <summary>
    /// Registers all metadata enrichment services: ISBN detection, provider clients
    /// (named HttpClients with 10 s timeout), aggregator, confidence merge,
    /// apply, quality score, health dashboard, write-back, and batch enrichment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="libraryRoot">
    /// The validated library root directory, used by the write-back service for
    /// path-traversal validation.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMetadataEnrichment(
        this IServiceCollection services,
        string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        // ISBN detection.
        services.AddSingleton<IIsbnDetectionService, IsbnDetectionService>();

        // HTTP clients — named, routed through IHttpClientFactory (architecture rule SI-1).
        services.AddHttpClient<GoogleBooksProvider>(
            "GoogleBooks",
            client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/books/v1/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Ogma-Library/0.1 (+https://github.com/peterbamuhigire/Ogma-Library)");
            });

        services.AddHttpClient<OpenLibraryProvider>(
            "OpenLibrary",
            client =>
            {
                client.BaseAddress = new Uri("https://openlibrary.org/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Ogma-Library/0.1 (+https://github.com/peterbamuhigire/Ogma-Library)");
            });

        // Register both providers as IMetadataProvider implementations.
        services.AddSingleton<IMetadataProvider, GoogleBooksProvider>(sp =>
            sp.GetRequiredService<GoogleBooksProvider>());
        services.AddSingleton<IMetadataProvider, OpenLibraryProvider>(sp =>
            sp.GetRequiredService<OpenLibraryProvider>());

        // Aggregator.
        services.AddSingleton<IMetadataProviderAggregator, MetadataProviderAggregator>();

        // Confidence merge.
        services.AddSingleton<IConfidenceMergeService, ConfidenceMergeService>();

        // Quality score.
        services.AddSingleton<IMetadataQualityService, MetadataQualityService>();

        // Apply service (depends on quality service).
        services.AddSingleton<IMetadataApplyService, MetadataApplyService>();

        // Library health dashboard.
        services.AddSingleton<ILibraryHealthService, LibraryHealthService>();

        // Write-back.
        services.AddSingleton<IMetadataWriteBackService>(sp =>
            new PdfWriteBackService(
                sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Catalogue.CatalogueDbContext>>(),
                sp.GetRequiredService<ISidecarService>(),
                libraryRoot,
                sp.GetRequiredService<ILibrarySettingsService>()));

        // Batch enrichment orchestrator.
        services.AddSingleton<IBatchEnrichmentOrchestrator, BatchEnrichmentOrchestrator>();
        services.AddSingleton<IBookMetadataEnrichmentService, BookMetadataEnrichmentService>();

        return services;
    }
}
