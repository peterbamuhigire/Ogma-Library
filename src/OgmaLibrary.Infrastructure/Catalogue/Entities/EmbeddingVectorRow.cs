namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for an embedding vector derived from a search chunk
/// (HLD §3 — EmbeddingVectors table, FR-SEARCH-004). Populated by Phase 11.
/// </summary>
public sealed class EmbeddingVectorRow
{
    /// <summary>The stable vector identifier.</summary>
    public long VectorId { get; set; }

    /// <summary>FK to the source search chunk.</summary>
    public long ChunkId { get; set; }

    /// <summary>Name of the model that produced this embedding.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Version or local digest of the model that produced this embedding.</summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>Number of dimensions in the vector.</summary>
    public int DimensionCount { get; set; }

    /// <summary>The raw vector data as a BLOB (float array, little-endian).</summary>
    public byte[]? VectorBlob { get; set; }

    /// <summary>UTC timestamp when this vector was generated.</summary>
    public DateTimeOffset GeneratedAtUtc { get; set; }

    /// <summary>Navigation: the source chunk.</summary>
    public SearchChunkRow? Chunk { get; set; }
}
