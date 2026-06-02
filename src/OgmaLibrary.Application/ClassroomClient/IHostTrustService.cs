namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Evaluates and accepts Host certificate fingerprints for TOFU onboarding.</summary>
public interface IHostTrustService
{
    /// <summary>Evaluates a presented Host certificate fingerprint without changing stored pins.</summary>
    Task<HostTrustEvaluation> EvaluateAsync(
        ClassroomJoinRequest request,
        string presentedFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a user-accepted Host fingerprint after explicit TOFU confirmation.</summary>
    Task<HostTrustEvaluation> AcceptAsync(
        ClassroomJoinRequest request,
        string presentedFingerprint,
        CancellationToken cancellationToken = default);
}
