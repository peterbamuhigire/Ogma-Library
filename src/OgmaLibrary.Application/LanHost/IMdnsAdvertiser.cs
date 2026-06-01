namespace OgmaLibrary.Application.LanHost;

/// <summary>Advertises and withdraws the Host over mDNS/DNS-SD.</summary>
public interface IMdnsAdvertiser
{
    /// <summary>Registers the Host service record.</summary>
    Task StartAsync(MdnsServiceRecord record, CancellationToken cancellationToken = default);

    /// <summary>Deregisters the Host service record.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

