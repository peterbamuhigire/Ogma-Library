namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for extracted text from a single page
/// (HLD §3 — ExtractedPages table, FR-SEARCH-002). Populated by the ingestion
/// pipeline in Phase 05.
/// </summary>
public sealed class ExtractedPageRow
{
    /// <summary>The stable extracted page identifier.</summary>
    public long ExtractedPageId { get; set; }

    /// <summary>FK to the source book.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Zero-based page number.</summary>
    public int PageNumber { get; set; }

    /// <summary>The extracted plain text for this page.</summary>
    public string? TextContent { get; set; }

    /// <summary>Method used to extract the text (e.g. "PdfPig", "Tesseract").</summary>
    public string? ExtractionMethod { get; set; }

    /// <summary>UTC timestamp when this page was extracted.</summary>
    public DateTimeOffset? ExtractionUtc { get; set; }

    /// <summary>Navigation: the owning book.</summary>
    public BookRow? Book { get; set; }

    /// <summary>Navigation: search chunks derived from this page.</summary>
    public List<SearchChunkRow> SearchChunks { get; set; } = [];
}
