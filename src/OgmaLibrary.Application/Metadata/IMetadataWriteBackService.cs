namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// A single field difference between the current PDF DocInfo state and the accepted
/// enrichment values (FR-META-005, ADR-0008). Presented to the user for confirmation
/// before any write-back proceeds.
/// </summary>
/// <param name="FieldName">The metadata field name (e.g. "Title", "Author").</param>
/// <param name="OldValue">The current value in the PDF's DocInfo, or null if absent.</param>
/// <param name="NewValue">The accepted enrichment value to write.</param>
public sealed record FieldDiff(string FieldName, string? OldValue, string? NewValue);

/// <summary>
/// A token produced after a successful backup, used as a guard to ensure
/// write-back can only proceed after <see cref="IMetadataWriteBackService.PrepareBackupAsync"/>
/// has been called (FR-META-005, R1 reversibility).
/// </summary>
/// <param name="BackupAbsolutePath">Absolute path to the backup file.</param>
/// <param name="OriginalAbsolutePath">Absolute path to the original PDF file.</param>
/// <param name="OriginalSha256">SHA-256 hex digest of the original file before backup.</param>
public sealed record BackupToken(
    string BackupAbsolutePath,
    string OriginalAbsolutePath,
    string OriginalSha256);

/// <summary>
/// Implements the reversible PDF DocInfo write-back protocol (FR-META-005, ADR-0008,
/// NFR-PROD-010, R1). The sequence is: backup → diff → user confirm → write (temp) →
/// verify (PdfPig can open) → atomic rename → on any error restore original.
/// </summary>
/// <remarks>
/// <para>
/// The service targets only files whose absolute path is under the validated library
/// root. Any path-traversal attempt throws an <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// The original file is ALWAYS restored on failure; the backup is kept until the
/// caller explicitly disposes the session. Audit events are written for both
/// <c>WriteBackSucceeded</c> and <c>WriteBackFailed</c>.
/// </para>
/// </remarks>
public interface IMetadataWriteBackService
{
    /// <summary>
    /// Copies the original PDF to the sidecar Backups folder and returns a
    /// <see cref="BackupToken"/> that guards the subsequent write.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="absoluteFilePath">Absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BackupToken"/> for use in <see cref="WriteAsync"/>.</returns>
    Task<BackupToken> PrepareBackupAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a field-level diff between the current PDF DocInfo and the proposed
    /// accepted fields. Only fields with changed values are included.
    /// </summary>
    /// <param name="absoluteFilePath">Absolute path to the PDF file.</param>
    /// <param name="acceptedProposals">The accepted enrichment proposals.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of changed fields for user confirmation.</returns>
    Task<IReadOnlyList<FieldDiff>> BuildDiffAsync(
        string absoluteFilePath,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes accepted metadata into the PDF DocInfo after backup and user confirmation.
    /// On any failure after the write starts, the original is restored from backup.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="acceptedProposals">The accepted enrichment proposals.</param>
    /// <param name="backupToken">The token produced by <see cref="PrepareBackupAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> when the write failed
    /// and the original was restored (audit event written in both cases).
    /// </returns>
    Task<bool> WriteAsync(
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        BackupToken backupToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the original PDF from a previously prepared backup. The backup is
    /// retained after restoration so the operation can be audited and repeated.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="backupToken">The token returned by <see cref="PrepareBackupAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the verified restore succeeds.</returns>
    Task<bool> RestoreBackupAsync(
        string bookId,
        BackupToken backupToken,
        CancellationToken cancellationToken = default);
}
