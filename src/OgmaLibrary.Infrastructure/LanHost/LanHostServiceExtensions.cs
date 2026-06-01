using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Registers the Phase 16 LAN Host bounded context.</summary>
public static class LanHostServiceExtensions
{
    /// <summary>Adds LAN Host-mode services without opening any listener.</summary>
    public static IServiceCollection AddLanHostServices(this IServiceCollection services, string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHostModeSettingsRepository, HostModeSettingsRepository>();
        services.AddSingleton<IClientSessionService, ClientSessionService>();
        services.AddSingleton<ICertificateProvisioner>(_ => new LocalCertificateProvisioner(
            dataDirectory ?? OgmaLibrary.Infrastructure.Catalogue.CatalogueServiceExtensions.GetDefaultDataDirectory()));
        services.AddSingleton<IMdnsAdvertiser, MdnsAdvertiser>();
        services.AddSingleton<ILibraryHostService, LibraryHostService>();
        return services;
    }
}
