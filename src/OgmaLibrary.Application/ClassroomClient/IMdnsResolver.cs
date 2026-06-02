namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Discovers LAN Library Hosts advertised through mDNS/DNS-SD.</summary>
public interface IMdnsResolver
{
    /// <summary>Observable stream of Hosts discovered during active scans.</summary>
    IObservable<DiscoveredClassroomHost> Hosts { get; }

    /// <summary>Runs a bounded discovery scan and returns valid discovered Hosts.</summary>
    Task<IReadOnlyList<DiscoveredClassroomHost>> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
