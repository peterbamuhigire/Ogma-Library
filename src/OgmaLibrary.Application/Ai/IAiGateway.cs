using System.Globalization;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Single provider-neutral gateway for AI completions.</summary>
public interface IAiGateway
{
    /// <summary>Sends an AI request through the privacy, preview, consent, provider, and audit pipeline.</summary>
    Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken);
}

/// <summary>Builds exact payload previews and payload hashes for gateway audit.</summary>
public interface IAiPayloadBuilder
{
    /// <summary>Builds the exact payload preview that will be shown before off-device egress.</summary>
    AiPayloadPreview BuildPreview(AiRequest request);

    /// <summary>Computes a SHA-256 hash of the exact provider-neutral payload preview.</summary>
    string ComputePayloadHash(AiPayloadPreview preview);
}

/// <summary>Displays the exact outbound AI payload before a cloud call is sent.</summary>
public interface IAiPreviewGate
{
    /// <summary>Shows the payload preview and returns the user's decision.</summary>
    Task<AiPreviewDecision> ShowAsync(AiPayloadPreview preview, CancellationToken cancellationToken);
}

/// <summary>User decision from the payload-preview gate.</summary>
public enum AiPreviewDecision
{
    /// <summary>Cancel the AI call before any provider request is sent.</summary>
    Cancel = 0,

    /// <summary>Send this AI call only.</summary>
    Send = 1,

    /// <summary>Send this call and skip repeated previews for the current gateway session.</summary>
    RememberForSession = 2,
}

/// <summary>Estimates provider cost at request-close time.</summary>
public interface IAiCostCalculator
{
    /// <summary>Estimates the call cost in USD, if token data is available.</summary>
    decimal? EstimateCostUsd(AiRequest request, AiCompletion completion);
}

/// <summary>Formats AI cost estimates for UI display.</summary>
public interface IAiCostFormatter
{
    /// <summary>Formats a USD cost estimate using the supplied culture's number formatting.</summary>
    string FormatUsd(decimal? estimatedCostUsd, CultureInfo culture);
}

/// <summary>Thrown when AI is disabled by the active privacy tier.</summary>
public sealed class AiDisabledException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="AiDisabledException"/>.</summary>
    public AiDisabledException()
        : base("AI features are disabled for the active privacy tier.")
    {
    }
}

/// <summary>Thrown when a request exceeds the active privacy tier or provider boundary.</summary>
public sealed class AiTierViolationException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="AiTierViolationException"/>.</summary>
    public AiTierViolationException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when the payload preview is cancelled before provider egress.</summary>
public sealed class AiPreviewCancelledException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="AiPreviewCancelledException"/>.</summary>
    public AiPreviewCancelledException()
        : base("The AI payload preview was cancelled before provider egress.")
    {
    }
}

/// <summary>Thrown when an off-device AI call lacks active consent.</summary>
public sealed class AiConsentRequiredException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="AiConsentRequiredException"/>.</summary>
    public AiConsentRequiredException(AiPrivacyTier tier, string provider, string scope)
        : base($"AI consent is required for tier '{tier}', provider '{provider}', and scope '{scope}'.")
    {
        Tier = tier;
        Provider = provider;
        Scope = scope;
    }

    /// <summary>The requested privacy tier.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>The requested provider.</summary>
    public string Provider { get; }

    /// <summary>The requested consent scope.</summary>
    public string Scope { get; }
}
