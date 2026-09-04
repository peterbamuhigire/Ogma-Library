namespace OgmaLibrary.Application.Metadata;

/// <summary>One pending proposal included in a bulk-review preview.</summary>
public sealed record MetadataBulkReviewItem(
    long ProposalId,
    string BookId,
    string FieldName,
    string? BeforeValue,
    string? ProposedValue,
    string Source,
    double Confidence,
    int Version);

/// <summary>Validated, caller-reviewable batch of pending metadata proposals.</summary>
public sealed record MetadataBulkReviewPreview(
    string BatchId,
    IReadOnlyList<MetadataBulkReviewItem> Items,
    DateTimeOffset CreatedUtc);

/// <summary>Outcome for one proposal in an atomically applied batch.</summary>
public sealed record MetadataBulkDecisionResult(
    long ProposalId,
    string BookId,
    string FieldName,
    bool Applied,
    string? Error);

/// <summary>Result of an atomic bulk metadata decision.</summary>
public sealed record MetadataBulkReviewResult(
    string BatchId,
    string UndoToken,
    IReadOnlyList<MetadataBulkDecisionResult> Decisions,
    DateTimeOffset CompletedUtc)
{
    /// <summary>Whether every requested proposal was applied.</summary>
    public bool IsAtomicSuccess => Decisions.Count > 0 && Decisions.All(decision => decision.Applied);
}

/// <summary>Preview, atomically apply, and safely undo a metadata proposal batch.</summary>
public interface IMetadataBulkReviewService
{
    /// <summary>Builds a bounded preview from still-pending proposal versions.</summary>
    Task<MetadataBulkReviewPreview> PreviewAsync(
        IReadOnlyList<long> proposalIds,
        CancellationToken cancellationToken = default);

    /// <summary>Applies the unchanged preview atomically and returns a one-time undo token.</summary>
    Task<MetadataBulkReviewResult> ApplyAsync(
        MetadataBulkReviewPreview preview,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Undoes a batch only when its token is valid and no later edit conflicts.</summary>
    Task<bool> UndoAsync(
        string batchId,
        string undoToken,
        string actorId,
        CancellationToken cancellationToken = default);
}
