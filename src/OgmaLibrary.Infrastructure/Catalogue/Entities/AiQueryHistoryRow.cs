namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for local AI query history (HLD §3 — AiQueryHistory table,
/// FR-AI-009). Supports erasure via the <see cref="IsDeleted"/> soft-delete flag
/// (NFR-PROD-014).
/// </summary>
public sealed class AiQueryHistoryRow
{
    /// <summary>The stable query identifier.</summary>
    public long QueryId { get; set; }

    /// <summary>The natural-language query text.</summary>
    public string? QueryText { get; set; }

    /// <summary>The AI provider key (e.g. "openai", "anthropic", "ollama").</summary>
    public string? ProviderKey { get; set; }

    /// <summary>The model identifier used for this query.</summary>
    public string? ModelId { get; set; }

    /// <summary>The active privacy tier (0=Offline, 1=MetadataOnly, 2=ContentAware, 3=LocalOllama).</summary>
    public int PrivacyTier { get; set; }

    /// <summary>SHA-256 hash of the request payload, for audit without storing content.</summary>
    public string? RequestPayloadHash { get; set; }

    /// <summary>A brief summary of the AI response.</summary>
    public string? ResponseSummary { get; set; }

    /// <summary>Tokens consumed in the prompt.</summary>
    public int? TokensIn { get; set; }

    /// <summary>Tokens in the completion.</summary>
    public int? TokensOut { get; set; }

    /// <summary>Estimated cost in USD for this query.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>UTC timestamp when this query was submitted.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Soft-delete flag; set to erase without removing the row.</summary>
    public bool IsDeleted { get; set; }
}
