namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for a catalogued book (HLD §3 — Books table).
/// This type lives only in Infrastructure and is mapped to the database.
/// Distinct from the Domain <see cref="OgmaLibrary.Domain.Book"/> entity that the
/// application layer receives.
/// </summary>
public sealed class BookRow
{
    /// <summary>The stable book identity (stored as string).</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>The bibliographic title, or null until enriched.</summary>
    public string? Title { get; set; }

    /// <summary>Relative path from the library root (forward-slash separated).</summary>
    public string? RelativePath { get; set; }

    /// <summary>Lower-case hex SHA-256 content hash.</summary>
    public string? Sha256Hash { get; set; }

    /// <summary>File size in bytes at last scan.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Last-modified ticks (UTC) at last scan.</summary>
    public long? MtimeTicks { get; set; }

    /// <summary>Optional structural PDF fingerprint.</summary>
    public string? PdfFingerprint { get; set; }

    /// <summary>Normalized ISBN-13 digits (no hyphens), when known.</summary>
    public string? IsbnNormalized { get; set; }

    /// <summary>Digital Object Identifier, when known.</summary>
    public string? Doi { get; set; }

    /// <summary>Current lifecycle status (0=Active, 1=Unavailable, 2=Excluded).</summary>
    public int Status { get; set; }

    /// <summary>Optional FK to the Edition this book belongs to.</summary>
    public long? EditionId { get; set; }

    /// <summary>The reader's rating 1–5, when set.</summary>
    public int? Rating { get; set; }

    /// <summary>Search indexing status (0=NotIndexed, 1=Extracting, 2=Indexed, 3=Failed).</summary>
    public int IndexStatus { get; set; }

    /// <summary>Publication year, when known.</summary>
    public int? Year { get; set; }

    /// <summary>Navigation: book files belonging to this book.</summary>
    public List<BookFileRow> BookFiles { get; set; } = [];

    /// <summary>Navigation: join records linking this book to authors.</summary>
    public List<BookAuthorRow> BookAuthors { get; set; } = [];

    /// <summary>Navigation: join records linking this book to shelves.</summary>
    public List<ShelfBookRow> ShelfBooks { get; set; } = [];

    /// <summary>Navigation: metadata fields for this book.</summary>
    public List<BookMetadataFieldRow> MetadataFields { get; set; } = [];

    /// <summary>Navigation: reading progress for this book.</summary>
    public ReadingProgressRow? ReadingProgress { get; set; }

    /// <summary>Navigation: structured reading-memory journal for this book.</summary>
    public ReadingMemoryRow? ReadingMemory { get; set; }

    /// <summary>Navigation: bookmarks for this book.</summary>
    public List<BookmarkRow> Bookmarks { get; set; } = [];

    /// <summary>Navigation: annotations for this book.</summary>
    public List<AnnotationRow> Annotations { get; set; } = [];

    /// <summary>Navigation: optional edition.</summary>
    public EditionRow? Edition { get; set; }

    /// <summary>
    /// Metadata quality score in [0.0, 1.0] computed from field completeness and
    /// average confidence (FR-META-007). Updated by the quality service after any
    /// metadata write. Defaults to 0.0 until the first enrichment pass.
    /// </summary>
    public double QualityScore { get; set; }
}
