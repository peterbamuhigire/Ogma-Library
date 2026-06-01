using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Local Ollama embedding provider. Implementations must call only loopback
/// Ollama endpoints and must fail closed when Ollama is unavailable.
/// </summary>
public interface IOllamaEmbeddingProvider : IAiProvider
{
    /// <summary>
    /// Embeds <paramref name="text"/> with the requested local Ollama model.
    /// </summary>
    Task<OllamaEmbeddingResult> EmbedAsync(
        string text,
        string modelName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns whether the local Ollama service is reachable.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

/// <summary>Embedding response from a local Ollama provider.</summary>
public sealed record OllamaEmbeddingResult(
    string ModelName,
    string ModelVersion,
    float[] Vector);
