using System.Net;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Validates whether a remote client address may access Host mode.</summary>
internal interface ILanClientAddressPolicy
{
    /// <summary>Returns true when the address is loopback or LAN-private.</summary>
    bool IsAllowed(IPAddress? address);
}
