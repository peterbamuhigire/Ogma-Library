namespace OgmaLibrary.Application.Ai;

/// <summary>Privacy-minimized advisor feedback; raw prompts and answers are excluded.</summary>
public sealed record AdvisorFeedbackEntry(
    string FeedbackId,
    string RequestHash,
    int Rating,
    string? ReasonCode,
    DateTimeOffset SubmittedUtc);

/// <summary>Durable, consent-gated advisor feedback boundary.</summary>
public interface IAdvisorFeedbackService
{
    /// <summary>Stores feedback only after explicit user consent.</summary>
    Task<AdvisorFeedbackEntry> SubmitAsync(
        AdvisorFeedbackEntry entry,
        bool consentGranted,
        CancellationToken cancellationToken = default);

    /// <summary>Returns retained feedback without prompt or answer content.</summary>
    Task<IReadOnlyList<AdvisorFeedbackEntry>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Deletes entries older than the service retention window.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>Raised when feedback is submitted without explicit consent.</summary>
public sealed class AdvisorFeedbackConsentRequiredException : InvalidOperationException
{
    /// <summary>Initializes the consent-required failure.</summary>
    public AdvisorFeedbackConsentRequiredException()
        : base("Explicit consent is required before advisor feedback is stored.")
    {
    }
}
