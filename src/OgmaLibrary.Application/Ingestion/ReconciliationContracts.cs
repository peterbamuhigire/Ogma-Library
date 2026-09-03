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
