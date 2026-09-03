using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>Lifecycle status for one root-relative directory discovery attempt.</summary>
public enum DiscoveryDirectoryStatus
{
    /// <summary>The directory was selected for discovery.</summary>
    Started = 0,

    /// <summary>The directory was read completely.</summary>
    Completed = 1,

    /// <summary>The directory could not be read completely.</summary>
    Failed = 2,
}

/// <summary>Safe, user-visible diagnostics emitted while walking one directory.</summary>
/// <param name="RelativeDirectory">The forward-slash path relative to the library root.</param>
/// <param name="Status">The directory lifecycle status.</param>
/// <param name="ErrorCode">A stable, non-sensitive error code when the read was incomplete.</param>
/// <param name="FilesSeen">The number of PDF files observed in this directory.</param>
/// <param name="OccurredUtc">When the diagnostic was emitted.</param>
public sealed record DiscoveryDirectoryDiagnostic(
    string RelativeDirectory,
    DiscoveryDirectoryStatus Status,
    string? ErrorCode,
    int FilesSeen,
    DateTimeOffset OccurredUtc);

/// <summary>Result of one incremental discovery pass.</summary>
public sealed record DiscoveryScanResult(
    ScanSessionDescriptor Session,
    int FilesSeen,
    int ChangedFiles,
    int UnchangedFiles,
    int FailedFiles,
    DateTimeOffset CompletedUtc,
    IReadOnlyList<DiscoveryDirectoryDiagnostic> Diagnostics);

/// <summary>Durable incremental discovery coordinator.</summary>
public interface IIncrementalDiscoveryService
{
    /// <summary>Scans one available root and queues only changed observations.</summary>
    Task<DiscoveryScanResult> ScanAsync(
        LibraryRootId rootId,
        IReadOnlyList<string>? excludedFolders = null,
        CancellationToken cancellationToken = default);
}
