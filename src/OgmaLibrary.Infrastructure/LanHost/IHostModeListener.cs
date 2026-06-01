using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Owns the opt-in Host-mode HTTPS listener lifecycle.</summary>
internal interface IHostModeListener
{
    /// <summary>Starts the listener for the supplied Host-mode settings.</summary>
    Task StartAsync(
        HostModeSettings settings,
        string certificateFingerprint,
        string enrollmentCode,
        CancellationToken cancellationToken = default);

    /// <summary>Stops the listener and releases the bound port.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
