namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a search-index chunk derived from a page
/// (HLD §3 — SearchChunks table, FR-SEARCH-002). Populated by the search-index
/// pipeline in Phase 10.
/// </summary>
public sealed class SearchChunkRow
{
    /// <summary>The stable chunk identifier.</summary>
    public long ChunkId { get; set; }

    /// <summary>FK to the source book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>FK to the source extracted page, nullable.</summary>
    public long? ExtractedPageId { get; set; }

    /// <summary>Zero-based chunk index within the book.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>The text of this chunk.</summary>
    public string? ChunkText { get; set; }

    /// <summary>Source category (0=page, 1=note, 2=tag, 3=description, 4=toc).</summary>
    public int Source { get; set; }

    /// <summary>Token count for this chunk.</summary>
    public int TokenCount { get; set; }

    /// <summary>UTC timestamp when this chunk was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Versioned extraction artifact that produced this chunk.</summary>
    public long? ExtractionArtifactId { get; set; }

    /// <summary>Version of the indexer that produced this chunk.</summary>
    public string IndexVersion { get; set; } = "fts5-v1";

    /// <summary>Navigation: the source extracted page.</summary>
    public ExtractedPageRow? ExtractedPage { get; set; }

    /// <summary>Navigation: embedding vectors derived from this chunk.</summary>
    public List<EmbeddingVectorRow> EmbeddingVectors { get; set; } = [];
}
