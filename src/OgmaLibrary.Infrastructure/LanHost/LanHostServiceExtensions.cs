using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Registers the Phase 16 LAN Host bounded context.</summary>
public static class LanHostServiceExtensions
{
    /// <summary>Adds LAN Host-mode services without opening any listener.</summary>
    public static IServiceCollection AddLanHostServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHostModeSettingsRepository, InMemoryHostModeSettingsRepository>();
        services.AddSingleton<IClientSessionService, InMemoryClientSessionService>();
        services.AddSingleton<ICertificateProvisioner, StubCertificateProvisioner>();
        services.AddSingleton<IMdnsAdvertiser, NoopMdnsAdvertiser>();
        services.AddSingleton<ILibraryHostService, LibraryHostService>();
        return services;
    }
}
