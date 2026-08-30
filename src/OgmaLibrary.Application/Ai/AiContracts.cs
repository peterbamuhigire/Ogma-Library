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
    /// <summary>Maximum query text length accepted by the gateway contract.</summary>
    public const int MaxQueryTextLength = 4_096;

    /// <summary>Maximum number of metadata fields in one request.</summary>
    public const int MaxMetadataFields = 100;

    /// <summary>Maximum number of content chunks in one request.</summary>
    public const int MaxContentChunks = 50;

    /// <summary>Maximum text length of one content chunk.</summary>
    public const int MaxContentChunkLength = 8_192;

    /// <summary>Creates a provider-neutral AI request.</summary>
    public AiRequest(
        AiPrivacyTier tier,
        string provider,
        string model,
        string queryType,
        string? queryText,
        IReadOnlyDictionary<string, string>? metadataFields = null,
        IReadOnlyList<AiContentChunk>? contentChunks = null,
        string consentScope = "library:default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentScope);

        MetadataFields = metadataFields ?? new Dictionary<string, string>(StringComparer.Ordinal);
        ContentChunks = contentChunks ?? [];
        if (tier == AiPrivacyTier.MetadataOnly && ContentChunks.Count > 0)
        {
            throw new ArgumentException(
                "Content chunks are forbidden for metadata-only AI requests.",
                nameof(contentChunks));
        }
        if (queryText?.Length > MaxQueryTextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(queryText), "AI query text exceeds the local request limit.");
        }
        if (MetadataFields.Count > MaxMetadataFields)
        {
            throw new ArgumentOutOfRangeException(nameof(metadataFields), "AI metadata field count exceeds the local request limit.");
        }
        if (ContentChunks.Count > MaxContentChunks ||
            ContentChunks.Any(chunk => string.IsNullOrWhiteSpace(chunk.BookId) ||
                                       string.IsNullOrWhiteSpace(chunk.Source) ||
                                       chunk.Text.Length > MaxContentChunkLength))
        {
            throw new ArgumentOutOfRangeException(nameof(contentChunks), "AI content exceeds the local request limit.");
        }

        Tier = tier;
        Provider = provider;
        Model = model;
        QueryType = queryType;
        QueryText = queryText;
        ConsentScope = consentScope;
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

    /// <summary>Consent scope required before off-device egress.</summary>
    public string ConsentScope { get; }

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
    string QueryType,
    string? QueryText,
    IReadOnlyDictionary<string, string> MetadataFields,
    IReadOnlyList<AiContentChunk> ContentChunks)
{
    /// <summary>Approximate payload character count for UI display.</summary>
    public int CharacterCount =>
        QueryType.Length +
        (QueryText?.Length ?? 0) +
        MetadataFields.Sum(metadata => metadata.Key.Length + metadata.Value.Length) +
        ContentChunks.Sum(chunk => chunk.BookId.Length + chunk.Source.Length + chunk.Text.Length);
}
