using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Workers;

namespace OgmaLibrary.App;

/// <summary>
/// Startup tasks that must complete before the desktop shell can query services.
/// </summary>
public static class ApplicationStartup
{
    /// <summary>
    /// Applies catalogue migrations, recovers interrupted jobs, and starts
    /// background services before any catalogue-backed view model is resolved.
    /// </summary>
    /// <param name="services">The built application service provider.</param>
    /// <param name="cancellationToken">A token to cancel startup work.</param>
    /// <returns>A task representing the startup work.</returns>
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await services.GetRequiredService<CatalogueMigrator>()
            .ApplyAsync(cancellationToken)
            .ConfigureAwait(false);

        JobRecoveryService? recovery = services.GetService<JobRecoveryService>();
        if (recovery is not null)
        {
            await recovery.RecoverAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (IHostedService hostedService in services.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops hosted services before the desktop process exits.
    /// </summary>
    /// <param name="services">The built application service provider.</param>
    /// <param name="cancellationToken">A token to cancel shutdown work.</param>
    /// <returns>A task representing the shutdown work.</returns>
    public static async Task StopAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (IHostedService hostedService in services.GetServices<IHostedService>().Reverse())
        {
            await hostedService.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
