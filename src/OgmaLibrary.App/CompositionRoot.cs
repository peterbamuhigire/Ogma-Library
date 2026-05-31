using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.Application;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Workers;

namespace OgmaLibrary.App;

/// <summary>
/// The single composition root for Ogma Library (HLD §2.3). This is the only place
/// in the solution permitted to bind a concrete implementation to an interface, so
/// the single off-device egress chokepoint (SI-1) and the bounded-context contracts
/// are enforceable by inspecting one type.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Registers every bounded-context service with the dependency-injection
    /// container. As contexts come online in later phases their implementations are
    /// registered here and nowhere else.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOgmaLibrary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Cross-cutting: performance-budget instrumentation (Phase 02).
        services.AddSingleton<IBenchmarkContext, StopwatchBenchmarkContext>();

        // Localization: MVP English + French (Phase 02).
        services.AddSingleton<ILocalizationService, InMemoryLocalizationService>();

        // View models — registered after ingestion pipeline so DI resolves all deps.
        services.AddTransient<MainWindowViewModel>();

        // Phase 04 — Catalogue & Data Layer.
        // The data directory defaults to "Ogma Library Data" under OS app-data.
        string dataDirectory = CatalogueServiceExtensions.GetDefaultDataDirectory();
        services.AddCatalogueContext(
            dataDirectory: dataDirectory,
            libraryRoot: dataDirectory);

        // Phase 05 — Ingestion Pipeline (Infrastructure services).
        services.AddIngestionPipeline(dataDirectory: dataDirectory);

        // Phase 05 — Workers: background job worker + crash-recovery service.
        services.AddSingleton<JobRecoveryService>();
        services.AddHostedService<BookIngestionWorker>();

        // Bounded-context registrations (Reader, Search, AI,
        // Bookshelf, Settings & Security, Packaging) are added here in Phases 06+.
        return services;
    }
}
