using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Phase 16 scaffold settings repository; EF persistence lands with M016.</summary>
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

