using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.App;

/// <summary>
/// Startup tasks that must complete before the desktop shell can query services.
/// </summary>
public static class ApplicationStartup
{
    /// <summary>
    /// Applies catalogue migrations before any catalogue-backed view model is
    /// resolved. This keeps fresh and pre-existing empty SQLite files usable.
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
    }
}
