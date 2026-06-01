using System.Net;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Allows loopback fallback and RFC1918 IPv4 LAN clients only.</summary>
internal sealed class LanClientAddressPolicy : ILanClientAddressPolicy
{
    /// <inheritdoc />
    public bool IsAllowed(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        return LanBindAddressSelector.IsPrivateIpv4(address);
    }
}
