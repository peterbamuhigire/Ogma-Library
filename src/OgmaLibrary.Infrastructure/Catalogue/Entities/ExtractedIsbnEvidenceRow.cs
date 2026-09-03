namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Ranked, source-attributed ISBN evidence emitted by one extraction artifact.</summary>
public sealed class ExtractedIsbnEvidenceRow
{
    /// <summary>Database identifier.</summary>
    public long ExtractedIsbnEvidenceId { get; set; }

    /// <summary>Owning legacy catalogue book identity.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The versioned extraction artifact that produced this observation.</summary>
    public long ExtractionArtifactId { get; set; }

    /// <summary>Normalized ISBN-10 or ISBN-13 value.</summary>
    public string IsbnNormalized { get; set; } = string.Empty;

    /// <summary>ISBN kind: zero for ISBN-10 and one for ISBN-13.</summary>
    public int IdentifierKind { get; set; }

    /// <summary>Detection source from <see cref="OgmaLibrary.Application.Metadata.IsbnSource"/>.</summary>
    public int Source { get; set; }

    /// <summary>Zero-based rank among the retained candidates.</summary>
    public int Rank { get; set; }

    /// <summary>Whether this was the highest-priority validated candidate.</summary>
    public bool IsBest { get; set; }

    /// <summary>UTC time at which the observation was persisted.</summary>
    public DateTimeOffset DetectedUtc { get; set; }
}
