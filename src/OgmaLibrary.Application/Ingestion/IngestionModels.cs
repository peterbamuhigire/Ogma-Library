namespace OgmaLibrary.Application.Ingestion;

/// <summary>A PDF file discovered during a library scan (FR-LIB-002).</summary>
/// <param name="AbsolutePath">The absolute OS-native path to the file.</param>
/// <param name="RelativePath">The forward-slash path relative to the library root.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="MtimeTicks">The last-modified timestamp as UTC ticks.</param>
public sealed record DiscoveredFile(
    string AbsolutePath,
    string RelativePath,
    long SizeBytes,
    long MtimeTicks);

/// <summary>Scan phase labels for scan progress reporting (FR-LIB-001, NFR-PROD-005).</summary>
public enum ScanPhase
{
    /// <summary>Not yet started.</summary>
    Idle = 0,

    /// <summary>Recursively enumerating PDF files.</summary>
    Discovering = 1,

    /// <summary>Matching and registering files in the catalogue.</summary>
    Processing = 2,

    /// <summary>Generating thumbnails and spine strips.</summary>
    GeneratingAssets = 3,

    /// <summary>Scan completed successfully.</summary>
    Complete = 4,

    /// <summary>Scan completed with one or more per-file failures.</summary>
    PartialFailure = 5,

    /// <summary>Scan was cancelled by the user.</summary>
    Cancelled = 6,
}

/// <summary>A snapshot of scan progress for UI binding (NFR-PROD-005).</summary>
/// <param name="Phase">The current scan phase.</param>
/// <param name="FilesDiscovered">Total files found by discovery.</param>
/// <param name="FilesCompleted">Files fully processed so far.</param>
/// <param name="FilesFailed">Files that failed processing.</param>
/// <param name="IsCancellable">Whether a cancel is possible at this stage.</param>
public sealed record ScanProgressSnapshot(
    ScanPhase Phase,
    int FilesDiscovered,
    int FilesCompleted,
    int FilesFailed,
    bool IsCancellable)
{
    /// <summary>Progress in [0.0, 1.0]; 0.0 when no files discovered yet.</summary>
    public double ProgressPct =>
        FilesDiscovered == 0 ? 0.0
        : Math.Min(1.0, (FilesCompleted + FilesFailed) / (double)FilesDiscovered);
}

/// <summary>Health report data for one failure item (FR-LIB-007).</summary>
/// <param name="FilePath">The relative path of the failing file.</param>
/// <param name="ErrorMessage">The recorded error message.</param>
/// <param name="JobId">The Jobs row identifier for retry operations.</param>
/// <param name="FailedAtUtc">When the failure was recorded.</param>
public sealed record ScanFailureItem(
    string FilePath,
    string? ErrorMessage,
    long JobId,
    DateTimeOffset FailedAtUtc);

/// <summary>Aggregated scan health counts (FR-LIB-007).</summary>
/// <param name="FailedJobs">Jobs that failed for general reasons.</param>
/// <param name="PasswordProtected">Files detected as password-protected.</param>
/// <param name="MissingThumbnails">Books with no generated cover.</param>
/// <param name="MetadataGaps">Books missing Title or Author metadata.</param>
public sealed record ScanHealthReport(
    IReadOnlyList<ScanFailureItem> FailedJobs,
    IReadOnlyList<ScanFailureItem> PasswordProtected,
    IReadOnlyList<ScanFailureItem> MissingThumbnails,
    IReadOnlyList<ScanFailureItem> MetadataGaps);
