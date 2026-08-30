namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Versioned extraction-run manifest metadata.</summary>
public sealed class ExtractionArtifactRow
{
    /// <summary>Database identifier.</summary>
    public long ExtractionArtifactId { get; set; }

    /// <summary>Owning legacy catalogue book identity.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Source content hash, when known.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Parser and configuration contract version.</summary>
    public string ExtractorVersion { get; set; } = string.Empty;

    /// <summary>Artifact lifecycle state.</summary>
    public int Status { get; set; }

    /// <summary>Number of pages successfully processed.</summary>
    public int PagesProcessed { get; set; }

    /// <summary>Number of pages that failed independently.</summary>
    public int FailedPages { get; set; }

    /// <summary>SHA-256 hash of the deterministic output manifest.</summary>
    public string? ManifestHash { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC completion time.</summary>
    public DateTimeOffset? CompletedUtc { get; set; }
}
