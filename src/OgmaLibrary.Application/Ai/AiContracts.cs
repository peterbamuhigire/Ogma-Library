using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>One content chunk eligible for a content-aware AI request.</summary>
public sealed record AiContentChunk(
    string BookId,
    string Source,
    string Text);

/// <summary>Provider-neutral AI request passed through the gateway.</summary>
public sealed record AiRequest
{
    /// <summary>Creates a provider-neutral AI request.</summary>
    public AiRequest(
        AiPrivacyTier tier,
        string provider,
        string model,
        string queryType,
        string? queryText,
        IReadOnlyDictionary<string, string>? metadataFields = null,
        IReadOnlyList<AiContentChunk>? contentChunks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);

        MetadataFields = metadataFields ?? new Dictionary<string, string>(StringComparer.Ordinal);
        ContentChunks = contentChunks ?? [];
        if (tier == AiPrivacyTier.MetadataOnly && ContentChunks.Count > 0)
        {
            throw new ArgumentException(
                "Content chunks are forbidden for metadata-only AI requests.",
                nameof(contentChunks));
        }

        Tier = tier;
        Provider = provider;
        Model = model;
        QueryType = queryType;
        QueryText = queryText;
    }

    /// <summary>Privacy tier requested for this call.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>Provider key, for example openai, anthropic, deepseek, or ollama.</summary>
    public string Provider { get; }

    /// <summary>Provider model identifier.</summary>
    public string Model { get; }

    /// <summary>Use-case type, for example recommendation, reading-plan, or answer.</summary>
    public string QueryType { get; }

    /// <summary>User query text, if applicable.</summary>
    public string? QueryText { get; }

    /// <summary>Metadata fields allowed in Tier-1 payloads.</summary>
    public IReadOnlyDictionary<string, string> MetadataFields { get; }

    /// <summary>Selected text chunks allowed only in Tier-2 content-aware payloads.</summary>
    public IReadOnlyList<AiContentChunk> ContentChunks { get; }
}

/// <summary>Provider-neutral AI completion returned by the active provider.</summary>
public sealed record AiCompletion(
    string Text,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? PromptCacheTokens = null,
    bool IsLocal = false);

/// <summary>Exact payload preview shown before cloud egress.</summary>
public sealed record AiPayloadPreview(
    AiPrivacyTier Tier,
    string Provider,
    string Model,
    IReadOnlyDictionary<string, string> MetadataFields,
    IReadOnlyList<AiContentChunk> ContentChunks)
{
    /// <summary>Approximate payload character count for UI display.</summary>
    public int CharacterCount =>
        MetadataFields.Sum(metadata => metadata.Key.Length + metadata.Value.Length) +
        ContentChunks.Sum(chunk => chunk.BookId.Length + chunk.Source.Length + chunk.Text.Length);
}
