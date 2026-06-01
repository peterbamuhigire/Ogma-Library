namespace OgmaLibrary.Application.LanHost;

/// <summary>Reads and writes LAN Host-mode settings.</summary>
public interface IHostModeSettingsRepository
{
    /// <summary>Loads Host-mode settings, returning defaults if none are stored.</summary>
    Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves Host-mode settings.</summary>
    Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default);
}
