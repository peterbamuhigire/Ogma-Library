namespace OgmaLibrary.Application.Search;

/// <summary>
/// Full-text search over the Phase 10 FTS5 index.
/// </summary>
public interface IFtsIndexService
{
    /// <summary>
    /// Searches extracted text and indexed note/tag/description/TOC chunks.
    /// </summary>
    Task<IReadOnlyList<FtsSearchResult>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs SQLite FTS5 integrity-check against the derived full-text table.
    /// </summary>
    Task<FtsIntegrityResult> CheckIntegrityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Removes chunks whose book, page anchor, or completed extraction artifact
    /// is no longer valid. FTS delete triggers keep the derived table aligned.
    /// </summary>
    Task<FtsCleanupResult> CleanupStaleAsync(CancellationToken cancellationToken);
}

/// <summary>One full-text search hit.</summary>
public sealed record FtsSearchResult(
    string BookId,
    string? Title,
    string? Author,
    long ChunkId,
    int? PageIndex,
    int ChunkIndex,
    SearchChunkSource Source,
    string Snippet,
    double Score);

/// <summary>Result of checking the FTS5 derived index.</summary>
public sealed record FtsIntegrityResult(bool IsHealthy, string? ErrorMessage);

/// <summary>Summary of one stale full-text cleanup pass.</summary>
public sealed record FtsCleanupResult(int RemovedChunkCount, bool IntegrityHealthy, string? ErrorMessage);
