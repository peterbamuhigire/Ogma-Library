using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.Application;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Localization;

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

        // View models.
        services.AddTransient<MainWindowViewModel>();

        // Bounded-context registrations (Catalogue, Ingestion, Reader, Search, AI,
        // Bookshelf, Settings & Security, Packaging) are added here in Phases 04+.
        return services;
    }
}
