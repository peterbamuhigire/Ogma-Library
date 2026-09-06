namespace OgmaLibrary.Application.Search;

/// <summary>Frozen public identifiers for the Phase 26 search contract.</summary>
public static class SearchContractVersions
{
    /// <summary>Semantic response and degradation schema.</summary>
    public const string SemanticResponse = "semantic-search-v1";

    /// <summary>Metadata/full-text reciprocal-rank fusion contract.</summary>
    public const string CombinedFusion = "rrf-v1";

    /// <summary>Semantic/exact/reader-signal hybrid ranking contract.</summary>
    public const string HybridFusion = "hybrid-v1";

    /// <summary>Offline relevance-evaluation metric contract.</summary>
    public const string Evaluation = "search-retrieval-evaluation-v1";
}
