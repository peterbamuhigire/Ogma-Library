using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 mDNS resolver tests.</summary>
public sealed class MdnsResolverTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task MdnsResolver_EmitsDiscoveredHost_OnServiceRecord()
    {
        var backend = new FakeMdnsResolverBackend(
            new MdnsResolver.MdnsDiscoveredRecord(
                "Room 12 Library",
                "192.168.1.13",
                7473,
                new Dictionary<string, string>
                {
                    ["fp"] = Fingerprint,
                    ["addr"] = "192.168.1.13",
                    ["requires-auth"] = "true",
                }));
        var resolver = new MdnsResolver(new ClassroomJoinParser(), () => backend);
        var observer = new RecordingObserver();
        using IDisposable subscription = resolver.Hosts.Subscribe(observer);

        IReadOnlyList<DiscoveredClassroomHost> hosts = await resolver.DiscoverAsync(TimeSpan.FromMilliseconds(1));

        DiscoveredClassroomHost host = Assert.Single(hosts);
        Assert.Equal("Room 12 Library", host.DisplayName);
        Assert.Equal("192.168.1.13", host.Address);
        Assert.Equal(7473, host.Port);
        Assert.Equal(Fingerprint, host.CertificateFingerprint);
        Assert.Equal("true", host.Txt["requires-auth"]);
        Assert.Single(observer.Hosts);
        Assert.Equal(host.HostId, observer.Hosts[0].HostId);
        Assert.Equal(MdnsResolver.ServiceType, backend.ServiceType);
        Assert.True(backend.Disposed);
    }

    [Fact]
    public async Task MdnsResolver_FiltersRecordsWithoutValidFingerprint()
    {
        var backend = new FakeMdnsResolverBackend(
            new MdnsResolver.MdnsDiscoveredRecord(
                "Bad Library",
                "192.168.1.14",
                7473,
                new Dictionary<string, string> { ["fp"] = "not-a-fingerprint" }),
            new MdnsResolver.MdnsDiscoveredRecord(
                "Good Library",
                "192.168.1.15",
                7473,
                new Dictionary<string, string> { ["fp"] = Fingerprint }));
        var resolver = new MdnsResolver(new ClassroomJoinParser(), () => backend);

        IReadOnlyList<DiscoveredClassroomHost> hosts = await resolver.DiscoverAsync(TimeSpan.FromMilliseconds(1));

        DiscoveredClassroomHost host = Assert.Single(hosts);
        Assert.Equal("Good Library", host.DisplayName);
        Assert.Equal("192.168.1.15", host.Address);
    }

    [Fact]
    public void MdnsResolver_IsRegisteredInClassroomClientServices()
    {
        IMdnsResolver resolver = new ServiceCollection()
            .AddClassroomClientServices(Path.Combine(Path.GetTempPath(), $"ogma-classroom-mdns-{Guid.NewGuid():N}"))
            .BuildServiceProvider()
            .GetRequiredService<IMdnsResolver>();

        Assert.NotNull(resolver.Hosts);
    }

    private sealed class FakeMdnsResolverBackend : MdnsResolver.IMdnsResolverBackend
    {
        private readonly IReadOnlyList<MdnsResolver.MdnsDiscoveredRecord> _records;

        public FakeMdnsResolverBackend(params MdnsResolver.MdnsDiscoveredRecord[] records) =>
            _records = records;

        public event EventHandler<MdnsResolver.MdnsDiscoveredRecord>? RecordDiscovered;

        public string? ServiceType { get; private set; }

        public bool Disposed { get; private set; }

        public Task<IReadOnlyList<MdnsResolver.MdnsDiscoveredRecord>> DiscoverAsync(
            string serviceType,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServiceType = serviceType;
            foreach (MdnsResolver.MdnsDiscoveredRecord record in _records)
            {
                RecordDiscovered?.Invoke(this, record);
            }

            return Task.FromResult(_records);
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingObserver : IObserver<DiscoveredClassroomHost>
    {
        public List<DiscoveredClassroomHost> Hosts { get; } = [];

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(DiscoveredClassroomHost value) => Hosts.Add(value);
    }
}
