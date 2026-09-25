namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// A search-extraction problem for one book (or one page of it). Before Sept-23 Phase 06
/// (T06.4, K25) these were stored as <c>ExtractionFailed</c> rows in <c>Jobs</c>, whose
/// <c>RetryCount</c> grew per failed page and inflated the job attempt totals. Issues are
/// not jobs: they are never claimed, retried or counted as attempts.
/// </summary>
public sealed class ExtractionIssueRow
{
    /// <summary>Database identity.</summary>
    public long ExtractionIssueId { get; set; }

    /// <summary>The affected book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The zero-based page, or null for a whole-book problem.</summary>
    public int? PageIndex { get; set; }

    /// <summary>The book content hash the issue was observed against (empty when unknown).</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>A stable machine-readable issue code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>How many times the issue was observed.</summary>
    public int Occurrences { get; set; }

    /// <summary>The last redacted message (no paths or document text).</summary>
    public string? LastMessage { get; set; }

    /// <summary>When the issue was first observed.</summary>
    public DateTimeOffset FirstSeenUtc { get; set; }

    /// <summary>When the issue was last observed.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}
