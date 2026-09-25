using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>
/// Keeps LAN Host tests on the loopback interface. The production selector binds the
/// machine's LAN address and the production advertiser joins mDNS on every interface,
/// which opens non-loopback listeners and raises OS firewall prompts during test runs.
/// </summary>
internal static class LoopbackLanHostTestServices
{
    /// <summary>Replaces the bind selector with loopback and mDNS advertising with a recorder.</summary>
    public static IServiceCollection UseLoopbackLanHost(this IServiceCollection services)
    {
        services.RemoveAll<ILanBindAddressSelector>();
        services.RemoveAll<IMdnsAdvertiser>();
        services.AddSingleton<ILanBindAddressSelector>(new LoopbackLanBindAddressSelector());
        services.AddSingleton<IMdnsAdvertiser, RecordingMdnsAdvertiser>();
        return services;
    }

    private sealed class LoopbackLanBindAddressSelector : ILanBindAddressSelector
    {
        public IPAddress SelectBindAddress() => IPAddress.Loopback;
    }

    /// <summary>Records advertisement calls without touching the network.</summary>
    internal sealed class RecordingMdnsAdvertiser : IMdnsAdvertiser
    {
        public MdnsServiceRecord? Current { get; private set; }

        public Task StartAsync(MdnsServiceRecord record, CancellationToken cancellationToken = default)
        {
            Current = record;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Current = null;
            return Task.CompletedTask;
        }
    }
}
