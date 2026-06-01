using System.Text;
using Makaretu.Dns;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>mDNS/DNS-SD advertiser for the opt-in LAN Host service.</summary>
internal sealed class MdnsAdvertiser : IMdnsAdvertiser, IDisposable
{
    private readonly Func<IMdnsBackend> _backendFactory;
    private IMdnsBackend? _backend;
    private MdnsAdvertisement? _advertisement;

    public MdnsAdvertiser()
        : this(() => new MakaretuMdnsBackend())
    {
    }

    internal MdnsAdvertiser(Func<IMdnsBackend> backendFactory)
    {
        _backendFactory = backendFactory ?? throw new ArgumentNullException(nameof(backendFactory));
    }

    /// <summary>The last advertised record, exposed for focused tests and status diagnostics.</summary>
    public MdnsServiceRecord? LastRecord { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(MdnsServiceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        MdnsAdvertisement advertisement = CreateAdvertisement(record);
        StopCurrentAdvertisement();

        _backend = _backendFactory();
        _backend.Advertise(advertisement);
        _advertisement = advertisement;
        LastRecord = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCurrentAdvertisement();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopCurrentAdvertisement();
    }

    internal static MdnsAdvertisement CreateAdvertisement(MdnsServiceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string serviceType = NormalizeServiceType(record.ServiceType);
        string instanceName = ValidateInstanceName(record.InstanceName);
        if (record.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(record), record.Port, "mDNS service port must be between 1 and 65535.");
        }

        var txt = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in record.Txt)
        {
            ValidateTxtPair(item.Key, item.Value);
            txt[item.Key] = item.Value;
        }

        return new MdnsAdvertisement(instanceName, serviceType, record.Port, txt);
    }

    private void StopCurrentAdvertisement()
    {
        try
        {
            if (_backend is not null && _advertisement is not null)
            {
                _backend.Unadvertise(_advertisement);
            }
        }
        finally
        {
            _backend?.Dispose();
            _backend = null;
            _advertisement = null;
            LastRecord = null;
        }
    }

    private static string NormalizeServiceType(string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            throw new ArgumentException("mDNS service type is required.", nameof(serviceType));
        }

        string normalized = serviceType.Trim();
        if (normalized.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".local".Length];
        }

        if (!normalized.StartsWith('_') ||
            (!normalized.EndsWith("._tcp", StringComparison.Ordinal) &&
             !normalized.EndsWith("._udp", StringComparison.Ordinal)))
        {
            throw new ArgumentException("mDNS service type must look like '_name._tcp' or '_name._udp'.", nameof(serviceType));
        }

        return normalized;
    }

    private static string ValidateInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            throw new ArgumentException("mDNS instance name is required.", nameof(instanceName));
        }

        string normalized = instanceName.Trim();
        if (Encoding.UTF8.GetByteCount(normalized) > 63)
        {
            throw new ArgumentException("mDNS instance name must be 63 UTF-8 bytes or fewer.", nameof(instanceName));
        }

        return normalized;
    }

    private static void ValidateTxtPair(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("mDNS TXT key is required.", nameof(key));
        }

        int length = Encoding.UTF8.GetByteCount($"{key}={value}");
        if (length > 255)
        {
            throw new ArgumentException("mDNS TXT records must be 255 UTF-8 bytes or fewer.", nameof(value));
        }
    }

    internal interface IMdnsBackend : IDisposable
    {
        void Advertise(MdnsAdvertisement advertisement);

        void Unadvertise(MdnsAdvertisement advertisement);
    }

    private sealed class MakaretuMdnsBackend : IMdnsBackend
    {
        private readonly ServiceDiscovery _serviceDiscovery = new();

        public void Advertise(MdnsAdvertisement advertisement)
        {
            ServiceProfile profile = ToProfile(advertisement);
            _serviceDiscovery.Advertise(profile);
            _serviceDiscovery.Announce(profile);
        }

        public void Unadvertise(MdnsAdvertisement advertisement)
        {
            _serviceDiscovery.Unadvertise(ToProfile(advertisement));
        }

        public void Dispose()
        {
            _serviceDiscovery.Dispose();
        }

        private static ServiceProfile ToProfile(MdnsAdvertisement advertisement)
        {
            var profile = new ServiceProfile(
                advertisement.InstanceName,
                advertisement.ServiceType,
                (ushort)advertisement.Port);

            foreach (KeyValuePair<string, string> item in advertisement.Txt)
            {
                profile.AddProperty(item.Key, item.Value);
            }

            return profile;
        }
    }
}
