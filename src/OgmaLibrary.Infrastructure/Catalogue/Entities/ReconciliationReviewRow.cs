namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable review item created when a relocation cannot be chosen safely.</summary>
public sealed class ReconciliationReviewRow
{
    /// <summary>Database identifier.</summary>
    public long ReconciliationReviewId { get; set; }

    /// <summary>The owning root.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>The occurrence whose relocation requires review.</summary>
    public string FileOccurrenceId { get; set; } = string.Empty;

    /// <summary>Stable review reason.</summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Root-relative candidate paths, retained only in local catalogue data.</summary>
    public string CandidatePathsJson { get; set; } = "[]";

    /// <summary>0=pending, 1=accepted, 2=rejected.</summary>
    public int Status { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC decision time, when reviewed.</summary>
    public DateTimeOffset? DecidedUtc { get; set; }
}
