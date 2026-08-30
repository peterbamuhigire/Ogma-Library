using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
using OgmaLibrary.App.ViewModels;

namespace OgmaLibrary.App.Composition;

internal sealed class StartupModule : IOgmaModuleRegistrar
{
    public string Name => "startup";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddSingleton<CatalogueMigrationStartupTask>();
        services.AddSingleton<JobRecoveryStartupTask>();
        services.AddSingleton<HostedServicesStartupTask>();
        services.AddSingleton<IApplicationStartupTask>(sp =>
            sp.GetRequiredService<CatalogueMigrationStartupTask>());
        services.AddSingleton<IApplicationStartupTask>(sp =>
            sp.GetRequiredService<JobRecoveryStartupTask>());
        services.AddSingleton<IApplicationStartupTask>(sp =>
            sp.GetRequiredService<HostedServicesStartupTask>());
        services.AddSingleton<IStartupCapabilityProbe, StartupCapabilityProbe>();
        services.AddSingleton<IApplicationStartupCoordinator, ApplicationStartupCoordinator>();
        services.AddSingleton<StartupShellViewModel>();
    }
}
