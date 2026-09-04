using OgmaLibrary.Application.Search;

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

    /// <summary>Hash of the source chunk and version tuple.</summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>Extractor version represented by the source chunk.</summary>
    public string ExtractorVersion { get; set; } = "unknown";

    /// <summary>Chunker contract version represented by the source chunk.</summary>
    public string ChunkerVersion { get; set; } = SearchChunker.CurrentVersion;

    /// <summary>Search-index version represented by the source chunk.</summary>
    public string IndexVersion { get; set; } = "fts5-v1";

    /// <summary>Provider identity that generated the vector.</summary>
    public string ProviderKey { get; set; } = "ollama";

    /// <summary>Whether this derived vector has been logically invalidated.</summary>
    public bool IsTombstoned { get; set; }

    /// <summary>UTC time when this vector was logically invalidated.</summary>
    public DateTimeOffset? TombstonedUtc { get; set; }

    /// <summary>Navigation: the source chunk.</summary>
    public SearchChunkRow? Chunk { get; set; }
}
