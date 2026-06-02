namespace OgmaLibrary.Application.LanHost;

/// <summary>Runtime state of the opt-in LAN Library Host.</summary>
public enum LibraryHostState
{
    /// <summary>No listener or discovery advertisement is running.</summary>
    Stopped = 0,

    /// <summary>Host mode is starting and has not accepted clients yet.</summary>
    Starting = 1,

    /// <summary>Host mode is active.</summary>
    Running = 2,

    /// <summary>Host mode failed to start or stopped after an error.</summary>
    Error = 3,
}

/// <summary>LAN content delivery mode selected by an administrator.</summary>
public enum HostContentDeliveryMode
{
    /// <summary>The host renders pages to images; PDF bytes stay on the host.</summary>
    PageRender = 0,

    /// <summary>The host may stream PDF files to authenticated clients.</summary>
    FileStream = 1,
}

/// <summary>Persisted Host-mode settings.</summary>
public sealed record HostModeSettings(
    bool IsEnabled,
    int Port,
    HostContentDeliveryMode ContentMode,
    string DisplayName);

/// <summary>Current Host-mode status for the Settings > Sharing panel.</summary>
public sealed record LibraryHostStatus(
    LibraryHostState State,
    int Port,
    int ConnectedClientCount,
    string? CertificateFingerprint,
    string? ErrorMessage,
    string? HostAddress = null,
    string? EnrollmentCode = null);

/// <summary>Result of provisioning or loading the Host certificate authority.</summary>
public sealed record CertificateProvisioningResult(
    string Fingerprint,
    DateTimeOffset NotAfterUtc);

/// <summary>mDNS/DNS-SD service record advertised while Host mode is running.</summary>
public sealed record MdnsServiceRecord(
    string ServiceType,
    string InstanceName,
    int Port,
    IReadOnlyDictionary<string, string> Txt);

/// <summary>Request to issue a LAN client session.</summary>
public sealed record ClientSessionRequest(
    string ClientId,
    string Role,
    TimeSpan Lifetime);

/// <summary>Issued LAN client session token metadata.</summary>
public sealed record ClientSessionResult(
    string Token,
    DateTimeOffset ExpiresUtc);

/// <summary>Resolved metadata for an active LAN client session.</summary>
public sealed record ClientSessionSnapshot(
    string TokenFingerprint,
    string ClientId,
    string Role,
    DateTimeOffset ExpiresUtc);

/// <summary>Opaque private-state sync blob stored by the LAN Host.</summary>
public sealed record HostProfileSyncBlob(
    string ContentType,
    byte[] Content,
    DateTimeOffset UpdatedUtc);

