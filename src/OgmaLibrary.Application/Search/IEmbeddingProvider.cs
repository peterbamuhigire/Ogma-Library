using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Provider-neutral text embedding contract for semantic search. An
/// implementation declares the exact model and version it runs, so stored
/// vectors carry their provenance and vectors from different models are never
/// compared. Implementations used for indexing must be local-only.
/// </summary>
public interface IEmbeddingProvider : IAiProvider
{
    /// <summary>Stable model identifier stored with every vector.</summary>
    string ModelId { get; }

    /// <summary>
    /// Exact model version (for example a pinned revision or tag). A change
    /// of version means existing vectors must be regenerated.
    /// </summary>
    string ModelVersion { get; }

    /// <summary>Length of every vector this provider returns.</summary>
    int Dimensions { get; }

    /// <summary>
    /// Returns whether the provider can embed now (model installed, service
    /// reachable). Must not throw for an absent capability.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Embeds <paramref name="text"/> as a search query or a stored passage.
    /// The returned vector has <see cref="Dimensions"/> finite components and
    /// unit length.
    /// </summary>
    Task<EmbeddingResult> EmbedAsync(
        string text,
        EmbeddingInputKind kind,
        CancellationToken cancellationToken);
}

/// <summary>
/// Role of the embedded text. Asymmetric models (for example E5) prefix
/// queries and passages differently; symmetric models ignore it.
/// </summary>
public enum EmbeddingInputKind
{
    /// <summary>A user's search query.</summary>
    Query = 0,

    /// <summary>A stored passage (a search chunk) of a book.</summary>
    Passage = 1,
}

/// <summary>Embedding vector with the provenance of the model that produced it.</summary>
/// <param name="ProviderKey">Provider that produced the vector.</param>
/// <param name="ModelId">Model identifier.</param>
/// <param name="ModelVersion">Exact model version.</param>
/// <param name="Vector">Unit-length embedding vector.</param>
public sealed record EmbeddingResult(
    string ProviderKey,
    string ModelId,
    string ModelVersion,
    float[] Vector);
