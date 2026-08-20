using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Composition;
using OgmaLibrary.App.Configuration;

namespace OgmaLibrary.App;

/// <summary>
/// The desktop application's single composition root. Registrations are grouped
/// into deterministic module registrars while all runtime binding remains in App.
/// </summary>
public static class CompositionRoot
{
    /// <summary>Ordered module names used by architecture and startup diagnostics.</summary>
    public static IReadOnlyList<string> RegisteredModuleNames { get; } =
        OgmaModuleCatalog.Modules.Select(module => module.Name).ToList();

    /// <summary>Registers Ogma Library using validated environment configuration.</summary>
    public static IServiceCollection AddOgmaLibrary(this IServiceCollection services) =>
        AddOgmaLibrary(services, OgmaRuntimeOptions.FromEnvironment());

    /// <summary>Registers Ogma Library using an explicit validated configuration.</summary>
    public static IServiceCollection AddOgmaLibrary(
        this IServiceCollection services,
        OgmaRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        services.AddSingleton(options);
        foreach (IOgmaModuleRegistrar module in OgmaModuleCatalog.Modules)
        {
            module.Register(services, options);
        }

        return services;
    }
}
