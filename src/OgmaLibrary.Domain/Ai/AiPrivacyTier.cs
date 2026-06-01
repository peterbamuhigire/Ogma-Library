namespace OgmaLibrary.Domain.Ai;

/// <summary>
/// Privacy tier for AI features. Offline is the default and sends no data.
/// </summary>
public enum AiPrivacyTier
{
    /// <summary>No AI provider calls are allowed.</summary>
    Offline = 0,

    /// <summary>Cloud calls may include metadata only.</summary>
    MetadataOnly = 1,

    /// <summary>Cloud calls may include selected book content after explicit consent.</summary>
    ContentAware = 2,

    /// <summary>Local Ollama calls only; no off-device egress.</summary>
    LocalOllama = 3,
}
