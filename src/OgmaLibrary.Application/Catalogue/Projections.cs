namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// A lightweight, LAN-safe summary projection of a book, used by the catalogue grid,
/// the 3D shelf, and (future) LAN clients (LAN-CLASSROOM-ARCHITECTURE.md §3).
/// All properties are plain value types or immutable collections; no EF Core
/// navigation properties.
/// </summary>
/// <param name="BookId">The stable book identity.</param>
/// <param name="Title">The bibliographic title, or null if not yet enriched.</param>
/// <param name="Authors">The display names of the book's authors.</param>
/// <param name="CoverRelativePath">Relative sidecar path to the cover image, or null.</param>
/// <param name="Status">The book's current lifecycle status (0=Active, 1=Unavailable, 2=Excluded).</param>
/// <param name="Rating">The reader's rating (1–5), or null if unrated.</param>
/// <param name="ShelfIds">Identifiers of the virtual shelves this book belongs to.</param>
/// <param name="ReadingProgressPct">Reading completion in [0.0, 100.0], or null if never opened.</param>
/// <param name="IsAvailable">Whether the physical file is currently present on disk.</param>
/// <param name="Year">Publication year, or null if unknown.</param>
/// <param name="Sha256Hash">SHA-256 hex digest of the file content, or null if not available.</param>
/// <param name="IsFavourite">Whether the reader marked the book as a favourite.</param>
/// <param name="RelativePath">Desktop-only library-root-relative source path; never expose in LAN projections.</param>
/// <param name="Processing">Non-sensitive indexing, embedding, OCR, and quality state.</param>
public sealed record BookSummaryProjection(
    string BookId,
    string? Title,
    IReadOnlyList<string> Authors,
    string? CoverRelativePath,
    int Status,
    int? Rating,
    IReadOnlyList<string> ShelfIds,
    double? ReadingProgressPct,
    bool IsAvailable,
    int? Year,
    string? Sha256Hash = null,
    bool IsFavourite = false,
    string? RelativePath = null,
    CatalogueProcessingProjection? Processing = null);

/// <summary>
/// Non-sensitive processing state used by catalogue badges and later 3D clients.
/// The values are read-only projections; no file paths or source content are
/// included.
/// </summary>
/// <param name="IndexStatus">Full-text indexing state.</param>
/// <param name="EmbeddingStatus">Semantic embedding state.</param>
/// <param name="QualityScore">Metadata quality score in [0, 1].</param>
/// <param name="IsOcrDerived">Whether searchable content includes OCR output.</param>
public sealed record CatalogueProcessingProjection(
    global::OgmaLibrary.Application.Search.SearchBookIndexStatus IndexStatus,
    global::OgmaLibrary.Application.Search.SearchEmbeddingStatus EmbeddingStatus,
    double QualityScore,
    bool IsOcrDerived)
{
    /// <summary>Whether at least one processing indicator has useful state.</summary>
    public bool HasProcessingState =>
        IndexStatus != global::OgmaLibrary.Application.Search.SearchBookIndexStatus.NotIndexed ||
        EmbeddingStatus != global::OgmaLibrary.Application.Search.SearchEmbeddingStatus.NotEmbedded;

    /// <summary>Whether the full-text index is current.</summary>
    public bool IsIndexed =>
        IndexStatus == global::OgmaLibrary.Application.Search.SearchBookIndexStatus.Indexed;

    /// <summary>Whether full-text extraction/indexing is in progress.</summary>
    public bool IsIndexing =>
        IndexStatus == global::OgmaLibrary.Application.Search.SearchBookIndexStatus.Extracting;

    /// <summary>Whether the latest full-text indexing attempt failed.</summary>
    public bool IsIndexFailed =>
        IndexStatus == global::OgmaLibrary.Application.Search.SearchBookIndexStatus.Failed;

    /// <summary>Whether semantic embeddings are current.</summary>
    public bool IsEmbedded =>
        EmbeddingStatus == global::OgmaLibrary.Application.Search.SearchEmbeddingStatus.Embedded;

    /// <summary>Whether semantic embedding generation is in progress.</summary>
    public bool IsEmbedding =>
        EmbeddingStatus == global::OgmaLibrary.Application.Search.SearchEmbeddingStatus.Embedding;

    /// <summary>Whether the latest semantic embedding attempt failed.</summary>
    public bool IsEmbeddingFailed =>
        EmbeddingStatus == global::OgmaLibrary.Application.Search.SearchEmbeddingStatus.Failed;

    /// <summary>Whether a non-default metadata quality score is available.</summary>
    public bool HasQualityScore => QualityScore > 0;
}

/// <summary>
/// A full detail projection of a book across all five metadata groups, used by the
/// book-detail panel (FR-CAT-004). All properties are plain value types or
/// JSON-serializable records; no EF Core navigation properties.
/// </summary>
/// <param name="BookId">The stable book identity.</param>
/// <param name="Title">The bibliographic title.</param>
/// <param name="Authors">The display names of the authors.</param>
/// <param name="Year">Publication year.</param>
/// <param name="Isbn">Normalized ISBN-13, or null.</param>
/// <param name="Doi">DOI string, or null.</param>
/// <param name="Rating">Reader rating 1–5, or null.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="CoverRelativePath">Sidecar-relative cover path, or null.</param>
/// <param name="RelativePath">Library-root-relative file path.</param>
/// <param name="Sha256Hash">SHA-256 hex digest of the file content.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="ReadingProgress">Current reading progress, or null.</param>
/// <param name="Annotations">Count of annotations.</param>
/// <param name="MetadataFields">All provenance-tracked metadata fields.</param>
/// <param name="ReadingMemory">Reading-memory summary, or null.</param>
/// <param name="IsOcrDerived">Whether searchable text for this book came from OCR.</param>
/// <param name="IsPasswordProtected">Whether the source PDF is password-protected.</param>
/// <param name="IsFavourite">Whether the reader marked the book as a favourite.</param>
/// <param name="IsAvailable">Whether at least one library file is currently present.</param>
public sealed record BookDetailProjection(
    string BookId,
    string? Title,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Isbn,
    string? Doi,
    int? Rating,
    int Status,
    string? CoverRelativePath,
    string? RelativePath,
    string? Sha256Hash,
    long? SizeBytes,
    ReadingProgressProjection? ReadingProgress,
    int Annotations,
    IReadOnlyList<MetadataFieldProjection> MetadataFields,
    ReadingMemorySummaryProjection? ReadingMemory = null,
    bool IsOcrDerived = false,
    bool IsPasswordProtected = false,
    bool IsFavourite = false,
    bool IsAvailable = true);

/// <summary>
/// Compact reading-memory data surfaced in the book-detail panel.
/// </summary>
/// <param name="Disposition">Reader disposition score 1-5, or null.</param>
/// <param name="KeyInsight">The latest key insight text, or null.</param>
/// <param name="UpdatedAtUtc">UTC timestamp of the latest reading-memory update.</param>
public sealed record ReadingMemorySummaryProjection(
    int? Disposition,
    string? KeyInsight,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>
/// Reading progress for a single book (FR-READ-001).
/// </summary>
/// <param name="BookId">The book identity.</param>
/// <param name="CurrentPage">Zero-based current page index.</param>
/// <param name="CompletionPct">Completion percentage in [0.0, 100.0].</param>
/// <param name="LastReadUtc">UTC timestamp of the last reading session, or null.</param>
/// <param name="Status">Reading status (0=Unread, 1=Reading, 2=Finished, 3=Abandoned).</param>
public sealed record ReadingProgressProjection(
    string BookId,
    int CurrentPage,
    double CompletionPct,
    DateTimeOffset? LastReadUtc,
    int Status);

/// <summary>
/// A virtual or smart shelf (FR-CAT-003).
/// </summary>
/// <param name="ShelfId">The stable shelf identity.</param>
/// <param name="Name">Display name.</param>
/// <param name="IsSmart">True if the shelf is query-defined (smart shelf).</param>
/// <param name="BookCount">Number of books on this shelf.</param>
public sealed record ShelfProjection(
    string ShelfId,
    string Name,
    bool IsSmart,
    int BookCount);

/// <summary>
/// A durable bookmark (FR-READ-007).
/// </summary>
/// <param name="BookmarkId">Stable bookmark identifier.</param>
/// <param name="BookId">The book this bookmark belongs to.</param>
/// <param name="Page">Zero-based page index.</param>
/// <param name="Label">Optional user label.</param>
/// <param name="CreatedUtc">Creation timestamp.</param>
public sealed record BookmarkProjection(
    long BookmarkId,
    string BookId,
    int Page,
    string? Label,
    DateTimeOffset CreatedUtc);

/// <summary>
/// A single metadata field with provenance tracking (FR-META-004).
/// </summary>
/// <param name="FieldName">The field name (e.g. "Title", "Author").</param>
/// <param name="Value">The field value.</param>
/// <param name="Source">The source provider.</param>
/// <param name="Confidence">Confidence score in [0.0, 1.0], or null.</param>
/// <param name="IsOverridden">Whether the user has manually overridden this value.</param>
public sealed record MetadataFieldProjection(
    string FieldName,
    string? Value,
    string? Source,
    double? Confidence,
    bool IsOverridden);

/// <summary>
/// Filter criteria for catalogue read-model queries.
/// </summary>
/// <param name="TitleContains">Optional title substring filter (case-insensitive).</param>
/// <param name="AuthorContains">Optional author name substring filter.</param>
/// <param name="ShelfId">Optional shelf ID filter.</param>
/// <param name="Status">Optional lifecycle status filter (null = all statuses).</param>
/// <param name="MaxResults">Maximum number of results to return (0 = unlimited).</param>
/// <param name="SkipCount">Number of ordered results to skip before taking the page.</param>
public sealed record CatalogueFilter(
    string? TitleContains = null,
    string? AuthorContains = null,
    string? ShelfId = null,
    int? Status = null,
    int MaxResults = 0,
    int SkipCount = 0);
