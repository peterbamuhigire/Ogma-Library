using System.Net;
using Makaretu.Dns;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>mDNS/DNS-SD resolver for Phase 17 Client/Classroom mode.</summary>
internal sealed class MdnsResolver : IMdnsResolver
{
    internal const string ServiceType = "_ogma-library._tcp";
    private readonly Func<IMdnsResolverBackend> _backendFactory;
    private readonly IClassroomJoinParser _joinParser;
    private readonly DiscoveryObservable _hosts = new();

    public MdnsResolver(IClassroomJoinParser joinParser)
        : this(joinParser, () => new MakaretuMdnsResolverBackend())
    {
    }

    internal MdnsResolver(IClassroomJoinParser joinParser, Func<IMdnsResolverBackend> backendFactory)
    {
        _joinParser = joinParser ?? throw new ArgumentNullException(nameof(joinParser));
        _backendFactory = backendFactory ?? throw new ArgumentNullException(nameof(backendFactory));
    }

    public IObservable<DiscoveredClassroomHost> Hosts => _hosts;

    public async Task<IReadOnlyList<DiscoveredClassroomHost>> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Discovery timeout must be positive.");
        }

        using IMdnsResolverBackend backend = _backendFactory();
        var discovered = new Dictionary<string, DiscoveredClassroomHost>(StringComparer.OrdinalIgnoreCase);
        backend.RecordDiscovered += (_, record) =>
        {
            if (TryCreateHost(record, out DiscoveredClassroomHost? host) && host is not null)
            {
                AddHost(discovered, host);
            }
        };

        IReadOnlyList<MdnsDiscoveredRecord> records = await backend.DiscoverAsync(
                ServiceType,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (MdnsDiscoveredRecord record in records)
        {
            if (TryCreateHost(record, out DiscoveredClassroomHost? host) && host is not null)
            {
                AddHost(discovered, host);
            }
        }

        return discovered.Values
            .OrderBy(host => host.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void AddHost(
        Dictionary<string, DiscoveredClassroomHost> discovered,
        DiscoveredClassroomHost host)
    {
        if (discovered.ContainsKey(host.HostId))
        {
            discovered[host.HostId] = host;
            return;
        }

        discovered[host.HostId] = host;
        _hosts.Publish(host);
    }

    private bool TryCreateHost(MdnsDiscoveredRecord record, out DiscoveredClassroomHost? host)
    {
        host = null;

        if (string.IsNullOrWhiteSpace(record.InstanceName) ||
            string.IsNullOrWhiteSpace(record.Address) ||
            record.Port is < 1 or > 65535 ||
            !record.Txt.TryGetValue("fp", out string? fingerprint))
        {
            return false;
        }

        string payload = $"ogma-lan://{record.Address}:{record.Port}/join?fp={Uri.EscapeDataString(fingerprint)}";
        if (!_joinParser.TryParse(payload, out ClassroomJoinRequest? request, out _) || request is null)
        {
            return false;
        }

        string hostId = CreateHostId(record.InstanceName, request.CertificateFingerprint);
        host = new DiscoveredClassroomHost(
            hostId,
            record.InstanceName.Trim(),
            request.Address,
            request.Port,
            request.CertificateFingerprint,
            new Dictionary<string, string>(record.Txt, StringComparer.OrdinalIgnoreCase));
        return true;
    }

    private static string CreateHostId(string instanceName, string fingerprint) =>
        $"{instanceName.Trim()}:{fingerprint[..12]}";

    internal interface IMdnsResolverBackend : IDisposable
    {
        event EventHandler<MdnsDiscoveredRecord>? RecordDiscovered;

        Task<IReadOnlyList<MdnsDiscoveredRecord>> DiscoverAsync(
            string serviceType,
            TimeSpan timeout,
            CancellationToken cancellationToken);
    }

    internal sealed record MdnsDiscoveredRecord(
        string InstanceName,
        string Address,
        int Port,
        IReadOnlyDictionary<string, string> Txt);

    private sealed class DiscoveryObservable : IObservable<DiscoveredClassroomHost>
    {
        private readonly List<IObserver<DiscoveredClassroomHost>> _observers = [];

        public IDisposable Subscribe(IObserver<DiscoveredClassroomHost> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        public void Publish(DiscoveredClassroomHost host)
        {
            foreach (IObserver<DiscoveredClassroomHost> observer in _observers.ToArray())
            {
                observer.OnNext(host);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<IObserver<DiscoveredClassroomHost>> _observers;
            private readonly IObserver<DiscoveredClassroomHost> _observer;

            public Subscription(
                List<IObserver<DiscoveredClassroomHost>> observers,
                IObserver<DiscoveredClassroomHost> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose() => _observers.Remove(_observer);
        }
    }

    private sealed class MakaretuMdnsResolverBackend : IMdnsResolverBackend
    {
        private readonly Dictionary<string, PendingRecord> _pending = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<MdnsDiscoveredRecord>? RecordDiscovered;

        public async Task<IReadOnlyList<MdnsDiscoveredRecord>> DiscoverAsync(
            string serviceType,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var mdns = new MulticastService();
            using var serviceDiscovery = new ServiceDiscovery(mdns);

            serviceDiscovery.ServiceInstanceDiscovered += (_, e) =>
            {
                string name = e.ServiceInstanceName.ToString();
                if (name.Contains(serviceType, StringComparison.OrdinalIgnoreCase))
                {
                    mdns.SendQuery(e.ServiceInstanceName, type: DnsType.SRV);
                    mdns.SendQuery(e.ServiceInstanceName, type: DnsType.TXT);
                }
            };

            mdns.AnswerReceived += (_, e) =>
            {
                foreach (ResourceRecord answer in e.Message.Answers.Concat(e.Message.AdditionalRecords))
                {
                    ApplyAnswer(answer, serviceType);
                }
            };

            mdns.Start();
            serviceDiscovery.QueryAllServices();
            try
            {
                await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            finally
            {
                mdns.Stop();
            }

            return _pending.Values
                .Select(item => item.ToRecord())
                .Where(record => record is not null)
                .Cast<MdnsDiscoveredRecord>()
                .ToArray();
        }

        public void Dispose()
        {
        }

        private void ApplyAnswer(ResourceRecord answer, string serviceType)
        {
            string name = answer.Name.ToString();
            if (!name.Contains(serviceType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PendingRecord pending = GetPending(name, serviceType);
            switch (answer)
            {
                case SRVRecord srv:
                    pending.Port = srv.Port;
                    pending.Address = TrimDnsName(srv.Target.ToString());
                    break;
                case TXTRecord txt:
                    foreach (string item in txt.Strings)
                    {
                        int separator = item.IndexOf('=');
                        if (separator > 0)
                        {
                            pending.Txt[item[..separator]] = item[(separator + 1)..];
                        }
                    }
                    break;
                case ARecord a:
                    pending.Address = a.Address.ToString();
                    break;
                case AAAARecord aaaa:
                    pending.Address = aaaa.Address.ToString();
                    break;
            }

            MdnsDiscoveredRecord? record = pending.ToRecord();
            if (record is not null)
            {
                RecordDiscovered?.Invoke(this, record);
            }
        }

        private PendingRecord GetPending(string recordName, string serviceType)
        {
            string instanceName = ExtractInstanceName(recordName, serviceType);
            if (!_pending.TryGetValue(instanceName, out PendingRecord? pending))
            {
                pending = new PendingRecord(instanceName);
                _pending[instanceName] = pending;
            }

            return pending;
        }

        private static string ExtractInstanceName(string recordName, string serviceType)
        {
            string normalized = TrimDnsName(recordName);
            string suffix = "." + serviceType;
            return normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? normalized[..^suffix.Length]
                : normalized;
        }

        private static string TrimDnsName(string value) => value.Trim().TrimEnd('.');

        private sealed class PendingRecord
        {
            public PendingRecord(string instanceName) => InstanceName = instanceName;

            public string InstanceName { get; }

            public string? Address { get; set; }

            public int Port { get; set; }

            public Dictionary<string, string> Txt { get; } = new(StringComparer.OrdinalIgnoreCase);

            public MdnsDiscoveredRecord? ToRecord()
            {
                if (string.IsNullOrWhiteSpace(Address) || Port is < 1 or > 65535 || Txt.Count == 0)
                {
                    return null;
                }

                if (Txt.TryGetValue("addr", out string? advertisedAddress) &&
                    !string.IsNullOrWhiteSpace(advertisedAddress))
                {
                    Address = advertisedAddress;
                }

                return new MdnsDiscoveredRecord(InstanceName, Address, Port, Txt);
            }
        }
    }
}
