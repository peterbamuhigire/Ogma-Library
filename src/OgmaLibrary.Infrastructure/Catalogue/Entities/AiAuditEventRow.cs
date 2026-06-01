namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>EF Core row for immutable AI gateway audit events.</summary>
public sealed class AiAuditEventRow
{
    /// <summary>Stable audit event identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the gateway call occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Privacy tier used for the call.</summary>
    public int Tier { get; set; }

    /// <summary>Provider key used for the call.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider model used for the call.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Prompt token count, if available.</summary>
    public int? PromptTokens { get; set; }

    /// <summary>Completion token count, if available.</summary>
    public int? CompletionTokens { get; set; }

    /// <summary>Prompt-cache token count, if available.</summary>
    public int? PromptCacheTokens { get; set; }

    /// <summary>Estimated cost in USD, if known.</summary>
    public decimal? EstimatedCostUsd { get; set; }

    /// <summary>SHA-256 hash of the exact payload sent.</summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the provider response.</summary>
    public string ResponseHash { get; set; } = string.Empty;

    /// <summary>Optional link to erasable query history.</summary>
    public string? QueryHistoryEntryId { get; set; }
}
