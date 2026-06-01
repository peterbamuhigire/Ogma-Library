namespace OgmaLibrary.Application.Search;

/// <summary>
/// Erases locally generated embedding vectors and resets semantic index state.
/// </summary>
public interface IEmbeddingErasureService
{
    /// <summary>
    /// Deletes all embedding vectors, resets book embedding statuses, and writes
    /// an audit event in one local transaction.
    /// </summary>
    Task<EmbeddingErasureResult> EraseAllAsync(CancellationToken cancellationToken);
}

/// <summary>Result of a completed embedding erasure operation.</summary>
public sealed record EmbeddingErasureResult(
    int VectorsErased,
    int BooksReset,
    DateTimeOffset ErasedAtUtc);

