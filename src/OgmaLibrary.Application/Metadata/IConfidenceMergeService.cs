namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// An alternative value for a single metadata field produced during the confidence
/// merge pass (FR-META-003). Displayed alongside the winning proposal in the review UI.
/// </summary>
/// <param name="Value">The alternative field value.</param>
/// <param name="Provider">The provider that supplied this value.</param>
/// <param name="Confidence">The field confidence for this alternative.</param>
public sealed record AlternativeFieldValue(string Value, string Provider, double Confidence);

/// <summary>
/// A per-field merge proposal produced by <see cref="IConfidenceMergeService"/>
/// (FR-META-003). Shown in the enrichment review panel where the user accepts,
/// rejects, or edits each field.
/// </summary>
/// <param name="FieldName">
/// The metadata field name (e.g. "Title", "Author", "ISBN", "Year",
/// "Publisher", "Description", "Categories").
/// </param>
/// <param name="ProposedValue">
/// The winning candidate value (highest <see cref="MergedConfidence"/>).
/// </param>
/// <param name="CurrentValue">
/// The current field value in the catalogue, if any.
/// </param>
/// <param name="MergedConfidence">
/// The confidence score for <see cref="ProposedValue"/>, computed as
/// <c>max(FieldConfidence(f, p)) over all providers p</c> where
/// <c>FieldConfidence = ProviderWeight × MatchScore × RecencyBonus</c>.
/// </param>
/// <param name="WinningProvider">The provider that supplied the winning value.</param>
/// <param name="Alternatives">
/// Other candidates for the same field, ordered by descending confidence.
/// </param>
/// <param name="ConfidenceModelVersion">Version of the calibration formula.</param>
public sealed record MergedMetadataProposal(
    string FieldName,
    string? ProposedValue,
    string? CurrentValue,
    double MergedConfidence,
    string WinningProvider,
    IReadOnlyList<AlternativeFieldValue> Alternatives,
    string ConfidenceModelVersion = "confidence-v1");

/// <summary>Canonical scope of a metadata field.</summary>
public enum MetadataFieldScope
{
    /// <summary>Describes the intellectual work across editions.</summary>
    Work = 0,

    /// <summary>Describes one publication/edition.</summary>
    Edition = 1,
}

/// <summary>Executable field dictionary for work/edition metadata scope.</summary>
public static class MetadataFieldPolicy
{
    /// <summary>
    /// Canonical metadata fields shared by provider results, review proposals,
    /// catalogue fields, and PDF write-back. The order is stable for UI and
    /// export consumers.
    /// </summary>
    public static IReadOnlyList<string> KnownFields { get; } =
    [
        "Title",
        "Author",
        "ISBN",
        "Year",
        "Publisher",
        "Description",
        "Categories",
        "CoverUrl",
        "AverageRating",
        "RatingsCount",
        "PageCount",
        "Language",
        "Subject",
        "Keywords",
    ];

    /// <summary>Returns true when the field is in the canonical dictionary.</summary>
    public static bool IsKnown(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return KnownFields.Contains(fieldName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the canonical scope for a normalized field name.</summary>
    public static MetadataFieldScope ScopeFor(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return fieldName.Trim().ToUpperInvariant() switch
        {
            "TITLE" or "AUTHOR" or "DESCRIPTION" or "CATEGORIES" => MetadataFieldScope.Work,
            "ISBN" or "YEAR" or "PUBLISHER" or "COVERURL" or "AVERAGERATING" or
            "RATINGSCOUNT" or "PAGECOUNT" or "LANGUAGE" or "SUBJECT" or "KEYWORDS" => MetadataFieldScope.Edition,
            _ => MetadataFieldScope.Edition,
        };
    }
}

/// <summary>
/// An accepted proposal ready to be written to <c>BookMetadataFields</c>
/// (FR-META-003, FR-META-004). The value may differ from
/// <see cref="MergedMetadataProposal.ProposedValue"/> when the user edited the field.
/// </summary>
/// <param name="FieldName">The metadata field name.</param>
/// <param name="AcceptedValue">The value the user accepted or typed.</param>
/// <param name="Source">The source provider, or "UserOverride" for manual edits.</param>
/// <param name="Confidence">The confidence to store (1.0 for user overrides).</param>
/// <param name="IsOverridden">True when the user typed a custom value.</param>
public sealed record AcceptedFieldProposal(
    string FieldName,
    string? AcceptedValue,
    string Source,
    double Confidence,
    bool IsOverridden);

/// <summary>
/// Applies the SRS §8.3 confidence merge formula to a set of provider lookup results
/// and produces a <see cref="MergedMetadataProposal"/> for every core metadata field
/// (FR-META-003).
/// </summary>
/// <remarks>
/// <para>
/// Formula (DECISIONS.md D-007, SRS §8.3):
/// <code>
/// FieldConfidence(f, provider) =
///   ProviderWeight(provider)          // GoogleBooks=0.85, OpenLibrary=0.80
///   × MatchScore(f, existingValue)    // 0.0–1.0 string similarity
///   × RecencyBonus(lookupAge)         // 1.0 &lt;7d, 0.85 &lt;30d, 0.70 older
///
/// MergedConfidence(f) = max over all providers of FieldConfidence(f, p)
/// </code>
/// </para>
/// <para>
/// A field with <c>IsOverridden = true</c> in the catalogue is treated as a user
/// manual override: its proposal carries <c>MergedConfidence = 1.0</c> and
/// <c>WinningProvider = "UserOverride"</c>, and is never replaced by provider data.
/// </para>
/// </remarks>
public interface IConfidenceMergeService
{
    /// <summary>
    /// Merges multiple provider results into one proposal per field.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="lookupResults">Provider results to merge (from all providers).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A list of <see cref="MergedMetadataProposal"/> records, one per core field
    /// (Title, Author, ISBN, Year, Publisher, Description, Categories).
    /// </returns>
    Task<IReadOnlyList<MergedMetadataProposal>> MergeAsync(
        string bookId,
        IReadOnlyList<ProviderMetadataResult> lookupResults,
        CancellationToken cancellationToken = default);
}
