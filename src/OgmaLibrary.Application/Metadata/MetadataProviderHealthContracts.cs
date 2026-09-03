namespace OgmaLibrary.Application.Metadata;

/// <summary>Observable provider-gateway health and quota state.</summary>
public sealed record MetadataProviderHealthSnapshot(
    string Provider,
    long WindowRequests,
    long WindowRejected,
    long ConsecutiveFailures,
    long TotalFailures,
    DateTimeOffset WindowStartedUtc,
    DateTimeOffset? CircuitOpenUntilUtc)
{
    /// <summary>Whether new calls are currently prevented by the circuit.</summary>
    public bool IsCircuitOpen => CircuitOpenUntilUtc is { } until && until > DateTimeOffset.UtcNow;
}

/// <summary>Tracks provider request quotas and failure-isolation state.</summary>
public interface IMetadataProviderHealth
{
    /// <summary>Reserves one request in the current provider quota window.</summary>
    bool TryReserve(string provider);

    /// <summary>Records a successful provider response.</summary>
    void RecordSuccess(string provider);

    /// <summary>Records a failed response and updates circuit state.</summary>
    void RecordFailure(string provider);

    /// <summary>Returns the current redacted provider health snapshot.</summary>
    MetadataProviderHealthSnapshot GetSnapshot(string provider);
}
