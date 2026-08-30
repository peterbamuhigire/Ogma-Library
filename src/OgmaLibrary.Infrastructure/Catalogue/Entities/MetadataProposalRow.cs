namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Persisted, reviewable metadata proposal.</summary>
public sealed class MetadataProposalRow
{
    /// <summary>Database identifier.</summary>
    public long MetadataProposalId { get; set; }

    /// <summary>Owning book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Canonical field name.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Candidate value.</summary>
    public string? ProposedValue { get; set; }

    /// <summary>Value visible when the proposal was created.</summary>
    public string? CurrentValue { get; set; }

    /// <summary>Calibrated confidence.</summary>
    public double Confidence { get; set; }

    /// <summary>Provider or extraction source.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Serialized bounded alternative values.</summary>
    public string AlternativesJson { get; set; } = "[]";

    /// <summary>Review lifecycle.</summary>
    public int Status { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC decision time.</summary>
    public DateTimeOffset? DecidedUtc { get; set; }
}
