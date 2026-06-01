using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>
/// Manages AI privacy tier, consent records, and payload previews.
/// </summary>
public interface IAiPrivacyService
{
    /// <summary>Gets the active AI privacy tier.</summary>
    AiPrivacyTier GetActiveTier();

    /// <summary>Sets the active AI privacy tier.</summary>
    Task SetTierAsync(AiPrivacyTier tier, CancellationToken cancellationToken);

    /// <summary>Records a consent grant or revocation.</summary>
    Task RecordConsentAsync(AiConsentRecord consent, CancellationToken cancellationToken);

    /// <summary>Returns whether active consent exists for the tier, provider, and scope.</summary>
    Task<bool> HasConsentAsync(
        AiPrivacyTier tier,
        string provider,
        string scope,
        CancellationToken cancellationToken);

    /// <summary>Builds the exact payload preview for a request before cloud egress.</summary>
    AiPayloadPreview BuildPayloadPreview(AiRequest request);
}
