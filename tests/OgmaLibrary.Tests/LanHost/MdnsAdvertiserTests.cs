using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 mDNS advertiser tests.</summary>
public sealed class MdnsAdvertiserTests
{
    [Fact]
    public async Task MdnsAdvertiser_StartRegisters_StopDeregisters()
    {
        var backend = new RecordingMdnsBackend();
        var advertiser = new MdnsAdvertiser(() => backend);
        var record = new MdnsServiceRecord(
            "_ogma-library._tcp.local",
            "Ogma School Library",
            7473,
            new Dictionary<string, string>
            {
                ["fp"] = new string('a', 64),
                ["requires-auth"] = "true",
            });

        await advertiser.StartAsync(record);

        Assert.Same(record, advertiser.LastRecord);
        Assert.Single(backend.Advertised);
        Assert.Equal("_ogma-library._tcp", backend.Advertised[0].ServiceType);
        Assert.Equal("Ogma School Library", backend.Advertised[0].InstanceName);
        Assert.Equal(7473, backend.Advertised[0].Port);

        await advertiser.StopAsync();

        Assert.Null(advertiser.LastRecord);
        Assert.Single(backend.Unadvertised);
        Assert.True(backend.Disposed);
    }

    [Fact]
    public void MdnsAdvertiser_ServiceRecord_ContainsFingerprintTxt()
    {
        var record = new MdnsServiceRecord(
            "_ogma-library._tcp.local",
            "Ogma Library",
            7473,
            new Dictionary<string, string>
            {
                ["fp"] = new string('b', 64),
                ["requires-auth"] = "true",
            });

        MdnsAdvertisement advertisement = MdnsAdvertiser.CreateAdvertisement(record);

        Assert.Equal(new string('b', 64), advertisement.Txt["fp"]);
        Assert.Equal("true", advertisement.Txt["requires-auth"]);
    }

    [Fact]
    public void MdnsAdvertiser_RejectsOversizedTxtRecord()
    {
        var record = new MdnsServiceRecord(
            "_ogma-library._tcp.local",
            "Ogma Library",
            7473,
            new Dictionary<string, string>
            {
                ["fp"] = new string('c', 300),
            });

        Assert.Throws<ArgumentException>(() => MdnsAdvertiser.CreateAdvertisement(record));
    }

    private sealed class RecordingMdnsBackend : MdnsAdvertiser.IMdnsBackend
    {
        public List<MdnsAdvertisement> Advertised { get; } = [];

        public List<MdnsAdvertisement> Unadvertised { get; } = [];

        public bool Disposed { get; private set; }

        public void Advertise(MdnsAdvertisement advertisement)
        {
            Advertised.Add(advertisement);
        }

        public void Unadvertise(MdnsAdvertisement advertisement)
        {
            Unadvertised.Add(advertisement);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
