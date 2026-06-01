using System.Net;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN client-address policy tests.</summary>
public sealed class LanClientAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("10.10.1.20", true)]
    [InlineData("172.20.1.20", true)]
    [InlineData("192.168.10.50", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("169.254.10.20", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void IsAllowed_AllowsLoopbackAndPrivateLanOnly(string address, bool expected)
    {
        var policy = new LanClientAddressPolicy();

        Assert.Equal(expected, policy.IsAllowed(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsAllowed_RejectsMissingAddress()
    {
        var policy = new LanClientAddressPolicy();

        Assert.False(policy.IsAllowed(null));
    }
}
