using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>Portable, user-visible representation of one configured library root.</summary>
public sealed record LibraryRootDescriptor(
    LibraryRootId Id,
    string DisplayName,
    string? CanonicalLocator,
    string? VolumeIdentity,
    LibraryRootStatus Status,
    LibraryRootPermissionStatus PermissionStatus,
    bool IsEnabled,
    bool AllowSymlinkTraversal,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastHealthCheckUtc,
    DateTimeOffset? LastSuccessfulScanUtc);

/// <summary>Result of a bounded root health probe.</summary>
public sealed record LibraryRootProbeResult(
    LibraryRootStatus Status,
    LibraryRootPermissionStatus PermissionStatus,
    string? VolumeIdentity);

/// <summary>Platform-specific path canonicalization and root health operations.</summary>
public interface ILibraryRootPlatformAdapter
{
    /// <summary>Returns an absolute canonical locator for a user-selected root.</summary>
    string CanonicalizeRoot(string path);

    /// <summary>Performs a bounded, non-recursive reachability and permission probe.</summary>
    LibraryRootProbeResult Probe(string canonicalLocator);
}

/// <summary>Manages durable library roots without deleting catalogue identities.</summary>
public interface ILibraryRootService
{
    /// <summary>Lists all configured roots, including disabled roots.</summary>
    Task<IReadOnlyList<LibraryRootDescriptor>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a root after canonicalization and an initial health probe.</summary>
    Task<LibraryRootDescriptor> AddAsync(
        string path,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or returns a root for a legacy settings path.</summary>
    Task<LibraryRootDescriptor> EnsureForLegacyPathAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Repoints an existing root while preserving its stable identity.</summary>
    Task<LibraryRootDescriptor> RelinkAsync(
        LibraryRootId rootId,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Enables or disables a root without deleting its occurrences.</summary>
    Task<LibraryRootDescriptor> SetEnabledAsync(
        LibraryRootId rootId,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>Refreshes health and permission state for one root.</summary>
    Task<LibraryRootDescriptor> RefreshHealthAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default);

    /// <summary>Records a successful scan without exposing paths to callers.</summary>
    Task RecordSuccessfulScanAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default);
}
