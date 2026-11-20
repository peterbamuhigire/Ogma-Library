namespace OgmaLibrary.Application.Search;

/// <summary>
/// Local Ollama embedding provider: an alias of <see cref="IEmbeddingProvider"/>
/// kept while callers migrate. Implementations must call only loopback Ollama
/// endpoints and must fail closed when Ollama is unavailable.
/// </summary>
public interface IOllamaEmbeddingProvider : IEmbeddingProvider
{
}
