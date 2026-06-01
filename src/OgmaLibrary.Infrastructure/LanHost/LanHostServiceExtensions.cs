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
        services.AddSingleton(_ => new LocalCertificateProvisioner(
            dataDirectory ?? OgmaLibrary.Infrastructure.Catalogue.CatalogueServiceExtensions.GetDefaultDataDirectory()));
        services.AddSingleton<ICertificateProvisioner>(sp => sp.GetRequiredService<LocalCertificateProvisioner>());
        services.AddSingleton<IHostServerCertificateProvider>(sp => sp.GetRequiredService<LocalCertificateProvisioner>());
        services.AddSingleton<IMdnsAdvertiser, MdnsAdvertiser>();
        services.AddSingleton<ILanBindAddressSelector, LanBindAddressSelector>();
        services.AddSingleton<ILanClientAddressPolicy, LanClientAddressPolicy>();
        services.AddSingleton<ILanPageRenderLimiter, LanPageRenderLimiter>();
        services.AddSingleton<ILanBookFileResolver>(sp => new LanBookFileResolver(
            sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Catalogue.CatalogueDbContext>>(),
            dataDirectory ?? OgmaLibrary.Infrastructure.Catalogue.CatalogueServiceExtensions.GetDefaultDataDirectory()));
        services.AddSingleton<ILanPageRenderer>(sp =>
        {
            var rendererFactory = sp.GetService<OgmaLibrary.Application.Reader.IPdfRendererFactory>();
            return rendererFactory is null
                ? new UnavailableLanPageRenderer()
                : new LanPageRenderer(sp.GetRequiredService<ILanBookFileResolver>(), rendererFactory);
        });
        services.AddSingleton<IHostModeListener, KestrelHostModeListener>();
        services.AddSingleton<ILibraryHostService, LibraryHostService>();
        return services;
    }
}
