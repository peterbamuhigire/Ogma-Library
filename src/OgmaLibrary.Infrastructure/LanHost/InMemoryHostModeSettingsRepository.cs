using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Deterministic in-memory host settings repository used by tests.</summary>
internal sealed class InMemoryHostModeSettingsRepository : IHostModeSettingsRepository
{
    private static readonly HostModeSettings Defaults =
        new(IsEnabled: false, Port: 7473, HostContentDeliveryMode.PageRender, "Ogma Library");

    private HostModeSettings _settings = Defaults;

    /// <inheritdoc />
    public Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_settings);
    }

    /// <inheritdoc />
    public Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        _settings = settings;
        return Task.CompletedTask;
    }
}

