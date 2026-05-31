using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// A per-book metadata quality score summary (FR-META-007).
/// </summary>
/// <param name="BookId">The catalogue book identifier.</param>
/// <param name="QualityScore">The computed score in [0.0, 1.0].</param>
/// <param name="PresentFieldCount">Number of core fields that have a value.</param>
/// <param name="TotalCoreFields">Total number of core fields evaluated (currently 7).</param>
/// <param name="AverageConfidence">Average confidence of enriched fields, or 0.0 if none.</param>
public sealed record BookQualityScore(
    string BookId,
    double QualityScore,
    int PresentFieldCount,
    int TotalCoreFields,
    double AverageConfidence);

/// <summary>
/// Computes and persists the metadata quality score for books (FR-META-007).
/// </summary>
/// <remarks>
/// <para>
/// Formula:
/// <code>
/// QualityScore(book) = (fieldPresenceScore × 0.6) + (averageConfidence × 0.4)
///
/// fieldPresenceScore = (count of present core fields) / 7
/// Core fields: Title, Author, ISBN, Year, Publisher, Cover, Description
/// </code>
/// </para>
/// </remarks>
public interface IMetadataQualityService
{
    /// <summary>
    /// Computes the quality score for a single book from its current metadata fields
    /// and persists the result to <c>Books.QualityScore</c>.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The computed <see cref="BookQualityScore"/>.</returns>
    Task<BookQualityScore> RecalculateAsync(
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns books whose quality score is at or below <paramref name="maxScore"/>.
    /// Used by the filter panel's "Quality below X%" chip (FR-META-007).
    /// </summary>
    /// <param name="maxScore">Maximum quality score threshold in [0.0, 1.0].</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Book summaries that fall below or at the threshold.</returns>
    IAsyncEnumerable<BookSummaryProjection> GetBelowThresholdAsync(
        double maxScore,
        CancellationToken cancellationToken = default);
}
