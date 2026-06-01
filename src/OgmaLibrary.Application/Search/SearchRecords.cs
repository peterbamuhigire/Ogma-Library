namespace OgmaLibrary.Application.Search;

/// <summary>
/// Indexing state for a catalogue book in the Search bounded context
/// (FR-SEARCH-002, FR-SEARCH-006).
/// </summary>
public enum SearchBookIndexStatus
{
    /// <summary>The book has not yet been indexed.</summary>
    NotIndexed = 0,

    /// <summary>The book is currently being extracted and indexed.</summary>
    Extracting = 1,

    /// <summary>The book's searchable text and metadata chunks are current.</summary>
    Indexed = 2,

    /// <summary>The latest extraction/indexing attempt failed.</summary>
    Failed = 3,
}

/// <summary>
/// Embedding-generation state for a catalogue book in the semantic search layer.
/// </summary>
public enum SearchEmbeddingStatus
{
    /// <summary>No embeddings have been generated for this book.</summary>
    NotEmbedded = 0,

    /// <summary>Embedding generation is currently in progress.</summary>
    Embedding = 1,

    /// <summary>All current search chunks have model-current embeddings.</summary>
    Embedded = 2,

    /// <summary>The latest embedding attempt failed.</summary>
    Failed = 3,
}

/// <summary>
/// Per-page text extraction quality used by Phase 10 indexing without depending
/// on the Reader bounded context.
/// </summary>
public enum SearchExtractionQuality
{
    /// <summary>Text extraction produced enough words to be considered complete.</summary>
    Full = 0,

    /// <summary>Text extraction produced some text, but the page may be incomplete.</summary>
    Partial = 1,

    /// <summary>No text was available on the page.</summary>
    Empty = 2,

    /// <summary>The page appears image-only and should be queued for OCR later.</summary>
    Scanned = 3,

    /// <summary>Extraction failed for this page, but indexing can continue.</summary>
    Failed = 4,
}

/// <summary>
/// Source category for a full-text search chunk. Phase 11 uses this value for
/// weighting and explanation, and Phase 16 can project it over LAN search.
/// </summary>
public enum SearchChunkSource
{
    /// <summary>Text extracted from a PDF page.</summary>
    Page = 0,

    /// <summary>Text from a user annotation note.</summary>
    Note = 1,

    /// <summary>Text from tag names.</summary>
    Tag = 2,

    /// <summary>Text from a book description or summary.</summary>
    Description = 3,

    /// <summary>Text from a PDF table-of-contents entry.</summary>
    Toc = 4,
}

/// <summary>Where a search result matched, used for Phase 11 explanation badges.</summary>
public enum MatchLocation
{
    /// <summary>The query matched the book title.</summary>
    Title = 0,

    /// <summary>The query matched an author name.</summary>
    Author = 1,

    /// <summary>The query matched a tag.</summary>
    Tag = 2,

    /// <summary>The query matched a description or summary.</summary>
    Description = 3,

    /// <summary>The query matched a table-of-contents entry.</summary>
    Toc = 4,

    /// <summary>The query matched a user annotation note.</summary>
    NotePage = 5,

    /// <summary>The query matched extracted page text.</summary>
    TextPage = 6,

    /// <summary>The query matched through semantic embedding similarity.</summary>
    Semantic = 7,
}

/// <summary>Human-readable confidence band derived from hybrid score.</summary>
public enum ConfidenceLabel
{
    /// <summary>Hybrid score is below 0.5.</summary>
    Low = 0,

    /// <summary>Hybrid score is at least 0.5 and below 0.8.</summary>
    Medium = 1,

    /// <summary>Hybrid score is at least 0.8.</summary>
    High = 2,
}

/// <summary>
/// A stored text extraction result for one zero-based page in one book.
/// </summary>
/// <param name="Id">Database identifier, or 0 before persistence.</param>
/// <param name="BookId">The stable catalogue book identity.</param>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="Text">Extracted plain text, or null when unavailable.</param>
/// <param name="Quality">Extraction quality classification.</param>
/// <param name="WordCount">Word count derived from <paramref name="Text"/>.</param>
/// <param name="ContentHash">Book content hash used for extraction staleness checks.</param>
/// <param name="ExtractedAtUtc">UTC timestamp of the extraction.</param>
public sealed record ExtractedPageRecord(
    long Id,
    string BookId,
    int PageIndex,
    string? Text,
    SearchExtractionQuality Quality,
    int WordCount,
    string? ContentHash,
    DateTimeOffset ExtractedAtUtc);

/// <summary>
/// A token-bounded search chunk that feeds SQLite FTS5 and future embeddings.
/// </summary>
/// <param name="Id">Database identifier, or 0 before persistence.</param>
/// <param name="BookId">The stable catalogue book identity.</param>
/// <param name="ExtractedPageId">Source extracted page identifier, if page-derived.</param>
/// <param name="PageIndex">Zero-based page index, if page-derived.</param>
/// <param name="ChunkIndex">Zero-based chunk sequence within the source book/source.</param>
/// <param name="Text">The searchable chunk text.</param>
/// <param name="TokenCount">Approximate token count for the chunk.</param>
/// <param name="Source">The source category for ranking and filters.</param>
/// <param name="CreatedAtUtc">UTC timestamp of chunk creation.</param>
public sealed record SearchChunkRecord(
    long Id,
    string BookId,
    long? ExtractedPageId,
    int? PageIndex,
    int ChunkIndex,
    string Text,
    int TokenCount,
    SearchChunkSource Source,
    DateTimeOffset CreatedAtUtc);
