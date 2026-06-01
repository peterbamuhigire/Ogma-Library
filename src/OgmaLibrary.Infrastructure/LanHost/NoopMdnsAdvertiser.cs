using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>No-op mDNS advertiser used until the Phase 16 mDNS adapter is wired.</summary>
internal sealed class NoopMdnsAdvertiser : IMdnsAdvertiser
{
    /// <summary>The last advertised record, exposed for focused scaffold tests.</summary>
    public MdnsServiceRecord? LastRecord { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(MdnsServiceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        LastRecord = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRecord = null;
        return Task.CompletedTask;
    }
}

