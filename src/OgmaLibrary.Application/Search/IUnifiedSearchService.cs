namespace OgmaLibrary.Application.Search;

/// <summary>
/// The search destination's single pipeline (Sept-23 Phase 13, ADR-0019): query parser →
/// structured field filters → metadata (exact, prefix and typo-tolerant) → FTS5 full text
/// (all terms, any order; quoted phrases stay phrases) → semantic results only when a
/// provider is available → reciprocal rank fusion, one result per catalogue book.
/// Versioned as <see cref="UnifiedSearchContract.Version"/>; the frozen v1 contracts stay
/// for their existing callers.
/// </summary>
public interface IUnifiedSearchService
{
    /// <summary>Runs one search. Never throws for malformed query text.</summary>
    Task<UnifiedSearchResponse> SearchAsync(
        string? queryText,
        int maxResults,
        CancellationToken cancellationToken);

    /// <summary>How many catalogue books have searchable text, for the destination header.</summary>
    Task<SearchIndexCoverage> GetCoverageAsync(CancellationToken cancellationToken);
}

/// <summary>Contract identifiers for the unified search pipeline.</summary>
public static class UnifiedSearchContract
{
    /// <summary>The response and fusion schema version.</summary>
    public const string Version = "unified-search-v1";

    /// <summary>Reciprocal-rank-fusion constant (the same k = 60 as rrf-v1).</summary>
    public const int RrfConstant = 60;
}

/// <summary>Whether semantic results took part in a response, as measured for that response.</summary>
public enum SemanticSearchState
{
    /// <summary>No embedding provider is available; results are keyword and full-text only.</summary>
    Unavailable = 0,

    /// <summary>A provider is available but no eligible embeddings exist yet.</summary>
    Preparing = 1,

    /// <summary>Semantic results were fused into this response.</summary>
    Active = 2,

    /// <summary>The query had only structured filters, so semantic search did not apply.</summary>
    NotApplicable = 3,
}

/// <summary>Where a result matched.</summary>
public enum UnifiedMatchKind
{
    /// <summary>The title.</summary>
    Title = 0,

    /// <summary>An author name.</summary>
    Author = 1,

    /// <summary>The ISBN.</summary>
    Isbn = 2,

    /// <summary>The publication year.</summary>
    Year = 3,

    /// <summary>A tag or subject.</summary>
    Tag = 4,

    /// <summary>A shelf name.</summary>
    Shelf = 5,

    /// <summary>The description.</summary>
    Description = 6,

    /// <summary>Page text.</summary>
    Page = 7,

    /// <summary>A reader note.</summary>
    Note = 8,

    /// <summary>A table-of-contents entry.</summary>
    Toc = 9,

    /// <summary>Similar meaning (semantic embeddings).</summary>
    Semantic = 10,
}

/// <summary>One place a result matched.</summary>
/// <param name="Kind">The field or text source.</param>
/// <param name="PageIndex">Zero-based page for text hits.</param>
/// <param name="IsFuzzy">True when the match tolerated a typo.</param>
/// <param name="IsOcrText">True when the text came from OCR of a scanned page (Phase 17).</param>
public sealed record UnifiedMatch(
    UnifiedMatchKind Kind,
    int? PageIndex = null,
    bool IsFuzzy = false,
    bool IsOcrText = false);

/// <summary>One catalogue book in a unified response.</summary>
/// <param name="BookId">The canonical catalogue book identity.</param>
/// <param name="Title">Display title resolved like the catalogue (title, metadata, then file name).</param>
/// <param name="Author">First display author, or null.</param>
/// <param name="Score">Fused reciprocal-rank score.</param>
/// <param name="Matches">Where the book matched, best first.</param>
/// <param name="Snippet">Best text snippet with highlighted spans, or null for metadata-only hits.</param>
/// <param name="PageJumpTarget">Reader target for the best page hit, or null.</param>
/// <param name="CorrectionSuggestion">The local spelling a typo-tolerant match used, if any.</param>
public sealed record UnifiedSearchResult(
    string BookId,
    string Title,
    string? Author,
    double Score,
    IReadOnlyList<UnifiedMatch> Matches,
    SearchSnippet? Snippet,
    SearchPageJumpTarget? PageJumpTarget,
    string? CorrectionSuggestion = null)
{
    /// <summary>The primary match shown with the result.</summary>
    public UnifiedMatch? PrimaryMatch => Matches.Count > 0 ? Matches[0] : null;

    /// <summary>Whether the best snippet came from OCR text.</summary>
    public bool IsOcrText => Matches.Any(match => match.IsOcrText);
}

/// <summary>Index coverage for the search header: "15 of 17 books searchable; 2 need OCR".</summary>
/// <param name="TotalBooks">Books in the catalogue.</param>
/// <param name="SearchableBooks">Books whose text can be searched.</param>
/// <param name="NeedOcrBooks">Scanned books that need OCR before their text is searchable.</param>
/// <param name="PendingBooks">Books whose text has not been read yet.</param>
public sealed record SearchIndexCoverage(
    int TotalBooks,
    int SearchableBooks,
    int NeedOcrBooks,
    int PendingBooks)
{
    /// <summary>No books yet.</summary>
    public static SearchIndexCoverage None { get; } = new(0, 0, 0, 0);
}

/// <summary>A unified search response; every flag is measured for this request.</summary>
/// <param name="Query">The parsed query.</param>
/// <param name="Results">Results, one per book, best first.</param>
/// <param name="Semantic">Whether semantic search contributed.</param>
/// <param name="UsedTypoTolerance">True when only typo-tolerant matches were found.</param>
/// <param name="Coverage">Index coverage at the time of the search.</param>
/// <param name="Elapsed">Time spent in the pipeline.</param>
/// <param name="ContractVersion">The contract version.</param>
public sealed record UnifiedSearchResponse(
    ParsedSearchQuery Query,
    IReadOnlyList<UnifiedSearchResult> Results,
    SemanticSearchState Semantic,
    bool UsedTypoTolerance,
    SearchIndexCoverage Coverage,
    TimeSpan Elapsed,
    string ContractVersion = UnifiedSearchContract.Version);
