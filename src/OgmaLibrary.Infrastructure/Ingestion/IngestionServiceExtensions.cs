using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Extension methods to register the Ingestion Pipeline Infrastructure services
/// (Phase 05) with the DI container. Background workers are registered separately
/// in the App composition root to maintain correct project layering.
/// </summary>
public static class IngestionServiceExtensions
{
    /// <summary>
    /// Registers all ingestion infrastructure services: settings, discovery, orchestrator,
    /// progress, health, thumbnail/spine, metadata extraction, registration,
    /// and unavailable flagging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataDirectory">The app-data directory for settings persistence.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIngestionPipeline(
        this IServiceCollection services,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddSingleton<ILibrarySettingsService>(_ => new LibrarySettingsService(dataDirectory));
        services.AddSingleton<ILibraryRootPlatformAdapter, FileSystemLibraryRootPlatformAdapter>();
        services.AddSingleton<ILibraryRootService, LibraryRootService>();
        services.AddSingleton<IProcessingStateService, ProcessingStateService>();
        services.AddSingleton<IIncrementalDiscoveryService, IncrementalDiscoveryService>();
        services.AddSingleton<IFilesystemReconciliationService, FilesystemReconciliationService>();
        services.TryAddSingleton<PdfWorkerClient>();
        services.AddSingleton<IPdfDiscoveryService, PdfDiscoveryService>();
        services.AddSingleton<IScanProgressService, ScanProgressService>();
        services.AddSingleton<IUnavailableFileFlagService, UnavailableFileFlagService>();
        services.AddSingleton<IBookRegistrationService, BookRegistrationService>();
        services.AddSingleton<IDirectPdfOpenService>(sp => new DirectPdfOpenService(
            sp.GetRequiredService<ILibrarySettingsService>(),
            sp.GetRequiredService<IBookIdentityService>(),
            sp.GetRequiredService<IBookRegistrationService>(),
            sp.GetRequiredService<CatalogueMigrator>(),
            sp.GetRequiredService<IDbContextFactory<CatalogueDbContext>>()));
        services.AddSingleton<IMetadataExtractionService, MetadataExtractionService>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<ISpineService, SpineService>();
        services.AddSingleton<IIngestionOrchestrator, IngestionOrchestrator>();
        services.AddSingleton<IScanHealthService, ScanHealthService>();

        return services;
    }
}
