using System.Net;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN bind-address selection tests.</summary>
public sealed class LanBindAddressSelectorTests
{
    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("192.168.1.25", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("169.254.10.20", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("8.8.8.8", false)]
    public void IsPrivateIpv4_ClassifiesLanRanges(string address, bool expected)
    {
        Assert.Equal(expected, LanBindAddressSelector.IsPrivateIpv4(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsPrivateIpv4_RejectsIpv6()
    {
        Assert.False(LanBindAddressSelector.IsPrivateIpv4(IPAddress.IPv6Loopback));
    }
}
