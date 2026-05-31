namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// Applies accepted metadata proposals to the catalogue, writing
/// <c>BookMetadataFields</c> rows and audit events (FR-META-003, FR-META-004,
/// CTRL-OGMA-018). Also triggers quality-score recalculation after each apply.
/// </summary>
public interface IMetadataApplyService
{
    /// <summary>
    /// Upserts the accepted field proposals for <paramref name="bookId"/> into
    /// <c>BookMetadataFields</c> with full provenance, writes an
    /// <c>AuditEvent(MetadataApplied)</c>, and recalculates the book's quality score.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="acceptedProposals">
    /// The proposals accepted by the user (may include user-edited overrides).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ApplyMergedMetadataAsync(
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken = default);
}
