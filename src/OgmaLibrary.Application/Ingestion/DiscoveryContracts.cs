using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>Result of one incremental discovery pass.</summary>
public sealed record DiscoveryScanResult(
    ScanSessionDescriptor Session,
    int FilesSeen,
    int ChangedFiles,
    int UnchangedFiles,
    int FailedFiles,
    DateTimeOffset CompletedUtc);

/// <summary>Durable incremental discovery coordinator.</summary>
public interface IIncrementalDiscoveryService
{
    /// <summary>Scans one available root and queues only changed observations.</summary>
    Task<DiscoveryScanResult> ScanAsync(
        LibraryRootId rootId,
        IReadOnlyList<string>? excludedFolders = null,
        CancellationToken cancellationToken = default);
}
