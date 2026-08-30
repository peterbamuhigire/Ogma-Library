using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>Persists conservative, path-free identity decisions and proposals.</summary>
public interface IIdentityDecisionService
{
    /// <summary>Evaluates two occurrences and records the versioned result once.</summary>
    Task<IdentityDecision> EvaluateAndRecordAsync(
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate,
        CancellationToken cancellationToken = default);

    /// <summary>Lists review-required decisions in deterministic creation order.</summary>
    Task<IReadOnlyList<IdentityDecision>> ListReviewRequiredAsync(
        CancellationToken cancellationToken = default);
}
