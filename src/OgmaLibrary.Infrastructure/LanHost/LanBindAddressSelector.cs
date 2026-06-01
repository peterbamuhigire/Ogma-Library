using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Chooses a private IPv4 LAN bind address for Host mode.</summary>
internal sealed class LanBindAddressSelector : ILanBindAddressSelector
{
    /// <inheritdoc />
    public IPAddress SelectBindAddress() =>
        GetCandidateAddresses().FirstOrDefault(IsPrivateIpv4) ?? IPAddress.Loopback;

    internal static bool IsPrivateIpv4(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private static IEnumerable<IPAddress> GetCandidateAddresses()
    {
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                !networkInterface.Supports(NetworkInterfaceComponent.IPv4))
            {
                continue;
            }

            foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return address.Address;
                }
            }
        }
    }
}
