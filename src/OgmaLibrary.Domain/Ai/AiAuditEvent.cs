namespace OgmaLibrary.Domain.Ai;

/// <summary>
/// Immutable audit record for one AI gateway call.
/// </summary>
public sealed record AiAuditEvent
{
    /// <summary>Creates an AI audit event.</summary>
    public AiAuditEvent(
        string id,
        DateTimeOffset occurredAt,
        AiPrivacyTier tier,
        string provider,
        string model,
        string payloadHash,
        string responseHash,
        int? promptTokens = null,
        int? completionTokens = null,
        int? promptCacheTokens = null,
        decimal? estimatedCostUsd = null,
        string? queryHistoryEntryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseHash);

        Id = id;
        OccurredAt = occurredAt;
        Tier = tier;
        Provider = provider;
        Model = model;
        PayloadHash = payloadHash;
        ResponseHash = responseHash;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        PromptCacheTokens = promptCacheTokens;
        EstimatedCostUsd = estimatedCostUsd;
        QueryHistoryEntryId = queryHistoryEntryId;
    }

    /// <summary>Stable audit event identifier.</summary>
    public string Id { get; }

    /// <summary>UTC timestamp when the gateway call occurred.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Privacy tier used for the call.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>Provider key used for the call.</summary>
    public string Provider { get; }

    /// <summary>Provider model used for the call.</summary>
    public string Model { get; }

    /// <summary>Prompt token count reported by the provider, if available.</summary>
    public int? PromptTokens { get; }

    /// <summary>Completion token count reported by the provider, if available.</summary>
    public int? CompletionTokens { get; }

    /// <summary>Prompt-cache token count reported by the provider, if available.</summary>
    public int? PromptCacheTokens { get; }

    /// <summary>Estimated call cost in USD, if known.</summary>
    public decimal? EstimatedCostUsd { get; }

    /// <summary>SHA-256 hash of the exact payload sent.</summary>
    public string PayloadHash { get; }

    /// <summary>SHA-256 hash of the provider response.</summary>
    public string ResponseHash { get; }

    /// <summary>Optional link to erasable query history.</summary>
    public string? QueryHistoryEntryId { get; }
}
