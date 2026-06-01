using System.Net;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Selects the local address used by the opt-in LAN Host listener.</summary>
internal interface ILanBindAddressSelector
{
    /// <summary>Returns the preferred LAN bind address, or loopback when no LAN address is available.</summary>
    IPAddress SelectBindAddress();
}
