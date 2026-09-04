namespace OgmaLibrary.Application.Search;

/// <summary>
/// Repository for embedding vectors derived from Phase 10 search chunks.
/// Vectors are a rebuildable local index, not source-of-truth catalogue data.
/// </summary>
public interface IEmbeddingVectorRepository
{
    /// <summary>
    /// Creates or replaces the embedding vector for a chunk/model/version tuple.
    /// </summary>
    Task<EmbeddingVectorRecord> CreateAsync(
        EmbeddingVectorRecord vector,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the stored vector for a chunk/model/version tuple, if present.
    /// </summary>
    Task<EmbeddingVectorRecord?> GetForChunkAsync(
        long chunkId,
        string modelName,
        string modelVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns every vector for chunks belonging to the given book.
    /// </summary>
    Task<IReadOnlyList<EmbeddingVectorRecord>> GetAllForBookAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>Counts vectors whose source fingerprint no longer matches its chunk.</summary>
    Task<int> GetStaleCountAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>Marks stale vectors for a book as tombstoned without deleting their audit state.</summary>
    Task<int> TombstoneStaleAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every stored embedding vector and returns the deleted row count.
    /// </summary>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Stored embedding vector for a search chunk.
/// </summary>
public sealed record EmbeddingVectorRecord(
    long Id,
    long ChunkId,
    string ModelName,
    string ModelVersion,
    float[] Vector,
    int DimensionCount,
    DateTimeOffset GeneratedAtUtc,
    string SourceHash = "",
    string ExtractorVersion = "unknown",
    string ChunkerVersion = SearchChunker.CurrentVersion,
    string IndexVersion = "fts5-v1",
    string ProviderKey = "ollama",
    bool IsTombstoned = false,
    DateTimeOffset? TombstonedUtc = null);
