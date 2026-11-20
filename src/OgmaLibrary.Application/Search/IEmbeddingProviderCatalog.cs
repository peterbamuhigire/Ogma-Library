namespace OgmaLibrary.Application.Search;

/// <summary>
/// Finds a configured embedding provider by model identity so searches can keep using an
/// existing generation while a new model regenerates (Sept-23 Phase 14, slice 4).
/// </summary>
public interface IEmbeddingProviderCatalog
{
    /// <summary>
    /// Returns the provider that produces <paramref name="providerKey"/>,
    /// <paramref name="modelId"/> and <paramref name="modelVersion"/> vectors when its last
    /// probe found it available; otherwise null. Never probes.
    /// </summary>
    IEmbeddingProvider? FindAvailable(string providerKey, string modelId, string modelVersion);
}
