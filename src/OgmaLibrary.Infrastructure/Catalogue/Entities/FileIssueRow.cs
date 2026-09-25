namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// A discovered file that was not catalogued because it is empty, not a PDF or
/// damaged (Sept-23 Phase 05, K21). Rows are data about the user's folder, never
/// book identities; they carry no content.
/// </summary>
public sealed class FileIssueRow
{
    /// <summary>Database identity.</summary>
    public long FileIssueId { get; set; }

    /// <summary>The owning library root, or null for a loose file.</summary>
    public string? LibraryRootId { get; set; }

    /// <summary>Root-relative, forward-slash path (absolute for a loose file).</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>The <c>FileValidity</c> reason (1=Empty, 2=NotAPdf, 3=Damaged).</summary>
    public int Reason { get; set; }

    /// <summary>Observed size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Observed UTC mtime ticks, used to re-check an ignored file when it changes.</summary>
    public long MtimeTicks { get; set; }

    /// <summary>Whether the user chose to ignore this file.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>UTC time the problem was first detected.</summary>
    public DateTimeOffset DetectedUtc { get; set; }

    /// <summary>UTC time the file was last checked.</summary>
    public DateTimeOffset LastCheckedUtc { get; set; }
}
