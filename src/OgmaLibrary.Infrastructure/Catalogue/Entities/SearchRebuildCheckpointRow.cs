namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable progress record for a resumable full-text rebuild.</summary>
public sealed class SearchRebuildCheckpointRow
{
    /// <summary>Database identifier.</summary>
    public long SearchRebuildCheckpointId { get; set; }

    /// <summary>Stable identifier for the rebuild attempt.</summary>
    public string RebuildId { get; set; } = string.Empty;

    /// <summary>Lifecycle state (0=pending, 1=running, 2=completed, 3=failed).</summary>
    public int Status { get; set; }

    /// <summary>Number of books presented to the pipeline.</summary>
    public int BooksAttempted { get; set; }

    /// <summary>Number of books indexed successfully.</summary>
    public int BooksIndexed { get; set; }

    /// <summary>Number of books that failed indexing.</summary>
    public int BooksFailed { get; set; }

    /// <summary>Number of chunks written by the pipeline.</summary>
    public int ChunksWritten { get; set; }

    /// <summary>UTC start timestamp.</summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>UTC timestamp of the last durable checkpoint update.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>UTC completion timestamp, when terminal.</summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Redacted failure information, when the rebuild failed.</summary>
    public string? ErrorMessage { get; set; }
}
