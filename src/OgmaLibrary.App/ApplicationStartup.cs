using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Startup;

namespace OgmaLibrary.App;

/// <summary>Facade for the recoverable desktop startup coordinator.</summary>
public static class ApplicationStartup
{
    /// <summary>Runs the asynchronous startup pipeline.</summary>
    public static Task<ApplicationStartupReport> InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IApplicationStartupCoordinator>()
            .InitializeAsync(cancellationToken);
    }

    /// <summary>Stops resources started by the startup coordinator.</summary>
    public static Task StopAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IApplicationStartupCoordinator>()
            .StopAsync(cancellationToken);
    }
}
