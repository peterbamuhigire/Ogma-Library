using Microsoft.Extensions.Logging;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>Structured log events for the unified search pipeline (Sept-23 Phase 13).</summary>
internal static partial class SearchLog
{
    /// <summary>One search completed; records sizes and timing only, never the query text.</summary>
    [LoggerMessage(EventId = 5130, EventName = "search.unified.completed", Level = LogLevel.Debug,
        Message = "Unified search returned {ResultCount} results ({MetadataCount} metadata, {TextCount} text, {SemanticCount} semantic) in {ElapsedMs} ms; semantic {SemanticState}")]
    public static partial void Completed(
        ILogger logger,
        int resultCount,
        int metadataCount,
        int textCount,
        int semanticCount,
        long elapsedMs,
        OgmaLibrary.Application.Search.SemanticSearchState semanticState);

    /// <summary>The optional semantic stage failed; keyword results are still returned.</summary>
    [LoggerMessage(EventId = 5131, EventName = "search.semantic.failed", Level = LogLevel.Warning,
        Message = "Semantic search failed; showing keyword and full-text results only")]
    public static partial void SemanticFailed(ILogger logger, Exception exception);

    /// <summary>The embedding-provider availability probe failed.</summary>
    [LoggerMessage(EventId = 5132, EventName = "search.semantic.probe_failed", Level = LogLevel.Debug,
        Message = "Semantic provider availability probe failed")]
    public static partial void ProbeFailed(ILogger logger, Exception exception);
}
