namespace OgmaLibrary.Application.Metadata;

/// <summary>One provider's candidate value for a metadata field.</summary>
public sealed record MetadataConflictCandidate(
    string Provider,
    string Value,
    double Confidence);

/// <summary>
/// A field for which successful providers returned more than one normalized value.
/// Candidates retain their display values and provenance for review UI consumers.
/// </summary>
public sealed record MetadataFieldConflict(
    string FieldName,
    IReadOnlyList<MetadataConflictCandidate> Candidates);

/// <summary>Deterministic field-level conflict report for one provider aggregation.</summary>
public sealed record MetadataConflictReport(
    IReadOnlyList<MetadataFieldConflict> Conflicts)
{
    /// <summary>Returns true when at least one field needs review.</summary>
    public bool HasConflicts => Conflicts.Count > 0;
}

/// <summary>
/// Detects disagreement between successful bibliographic providers without selecting
/// a winner. Selection remains the responsibility of the confidence merge service.
/// </summary>
public interface IMetadataConflictDetector
{
    /// <summary>Builds a deterministic field-level report from provider results.</summary>
    MetadataConflictReport Detect(IReadOnlyList<ProviderMetadataResult> results);
}
