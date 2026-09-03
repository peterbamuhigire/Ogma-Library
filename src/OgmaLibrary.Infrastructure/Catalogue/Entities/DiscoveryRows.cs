namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Latest local observation for a root-relative discovered file.</summary>
public sealed class DiscoveryObservationRow
{
    /// <summary>Database identifier.</summary>
    public long DiscoveryObservationId { get; set; }

    /// <summary>The owning root.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>Forward-slash normalized root-relative path.</summary>
    public string NormalizedRelativePath { get; set; } = string.Empty;

    /// <summary>The scan session that most recently observed this path.</summary>
    public long LastObservedScanSessionId { get; set; }

    /// <summary>Observed byte length.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Observed UTC mtime ticks.</summary>
    public long ModifiedUtcTicks { get; set; }

    /// <summary>Verified lower-case SHA-256 for the observed bytes, when computed.</summary>
    public string? Sha256Hash { get; set; }

    /// <summary>UTC first-observed timestamp.</summary>
    public DateTimeOffset FirstSeenUtc { get; set; }

    /// <summary>UTC most-recently-observed timestamp.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}

/// <summary>Latest completed discovery checkpoint for a root-relative directory.</summary>
public sealed class DirectoryCheckpointRow
{
    /// <summary>Database identifier.</summary>
    public long DirectoryCheckpointId { get; set; }

    /// <summary>The owning root.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>Forward-slash normalized root-relative directory.</summary>
    public string NormalizedRelativeDirectory { get; set; } = string.Empty;

    /// <summary>UTC completion timestamp.</summary>
    public DateTimeOffset LastCompletedUtc { get; set; }

    /// <summary>UTC time at which the current or last pass started.</summary>
    public DateTimeOffset? LastStartedUtc { get; set; }

    /// <summary>The scan session that owns the current or last checkpoint.</summary>
    public long? LastScanSessionId { get; set; }

    /// <summary>0=complete, 1=running, 2=failed or incomplete.</summary>
    public int ScanState { get; set; }

    /// <summary>Last completed directory used to resume an interrupted root pass.</summary>
    public string? ResumeCursorRelativeDirectory { get; set; }

    /// <summary>Number of PDF observations seen during the pass.</summary>
    public int LastObservedFileCount { get; set; }

    /// <summary>Stable error code when the directory was only partially read.</summary>
    public string? LastErrorCode { get; set; }
}
