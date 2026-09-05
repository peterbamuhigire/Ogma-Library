using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>Reason a reconciliation pass did or did not mutate availability.</summary>
public enum ReconciliationOutcome
{
    /// <summary>Presence evidence was applied.</summary>
    Applied = 0,

    /// <summary>The root was unavailable, so no occurrence was changed.</summary>
    RootUnavailable = 1,

    /// <summary>The scan was not complete enough to support absence decisions.</summary>
    IncompleteScan = 2,
}

/// <summary>Summary of one evidence-gated reconciliation pass.</summary>
public sealed record ReconciliationResult(
    long ScanSessionId,
    LibraryRootId RootId,
    ReconciliationOutcome Outcome,
    int RestoredOccurrences,
    int MarkedUnavailableOccurrences,
    int MovedOccurrences,
    int ReplacementOccurrences,
    DateTimeOffset EvaluatedUtc,
    int DeferredMissingOccurrences = 0,
    int AmbiguousOccurrences = 0,
    int InvalidatedStageExecutions = 0,
    IReadOnlyList<ReconciliationAuditSummary>? AuditSummary = null);

/// <summary>Counted, path-free explanation of reconciliation transitions.</summary>
public sealed record ReconciliationAuditSummary(string ReasonCode, int Count);

/// <summary>Reconciles occurrence availability from a completed root observation.</summary>
public interface IFilesystemReconciliationService
{
    /// <summary>Applies only a healthy, checkpoint-complete session’s evidence.</summary>
    Task<ReconciliationResult> ReconcileAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>The explicit decision applied to an ambiguous file relocation.</summary>
public enum ReconciliationReviewDecision
{
    /// <summary>Accept one validated candidate path and restore the occurrence.</summary>
    Accept = 1,

    /// <summary>Reject the relocation without guessing a new path.</summary>
    Reject = 2,
}

/// <summary>Path-safe presentation of one pending relocation review.</summary>
public sealed record ReconciliationReviewDescriptor(
    long ReviewId,
    string LibraryRootId,
    string FileOccurrenceId,
    string ReasonCode,
    IReadOnlyList<string> CandidatePaths,
    DateTimeOffset CreatedUtc);

/// <summary>Lists and decides ambiguous filesystem relocation reviews.</summary>
public interface IReconciliationReviewService
{
    /// <summary>Lists pending reviews, optionally scoped to one library root.</summary>
    Task<IReadOnlyList<ReconciliationReviewDescriptor>> ListPendingAsync(
        string? libraryRootId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an explicit accept/reject decision. Accept requires one candidate
    /// path from the persisted review and never accepts an arbitrary path.
    /// </summary>
    Task DecideAsync(
        long reviewId,
        ReconciliationReviewDecision decision,
        string? selectedRelativePath = null,
        CancellationToken cancellationToken = default);
}
