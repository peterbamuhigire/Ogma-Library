namespace OgmaLibrary.Application.Search;

/// <summary>Captured ranked results and relevance judgments for one search query.</summary>
public sealed record SearchEvaluationCase
{
    /// <summary>Creates a bounded search evaluation case.</summary>
    public SearchEvaluationCase(
        string queryId,
        string queryText,
        IReadOnlyList<string> rankedBookIds,
        IReadOnlySet<string> relevantBookIds,
        int k = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentNullException.ThrowIfNull(rankedBookIds);
        ArgumentNullException.ThrowIfNull(relevantBookIds);
        if (k is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "Evaluation K must be between 1 and 100.");
        }

        QueryId = queryId;
        QueryText = queryText;
        RankedBookIds = rankedBookIds;
        RelevantBookIds = relevantBookIds;
        K = k;
    }

    /// <summary>Stable query identifier.</summary>
    public string QueryId { get; }

    /// <summary>Original query text, retained for reproducibility.</summary>
    public string QueryText { get; }

    /// <summary>Book IDs in retrieval order before the evaluation cutoff.</summary>
    public IReadOnlyList<string> RankedBookIds { get; }

    /// <summary>Judged relevant book IDs for this query.</summary>
    public IReadOnlySet<string> RelevantBookIds { get; }

    /// <summary>Evaluation cutoff.</summary>
    public int K { get; }
}

/// <summary>Metrics for one captured search evaluation case.</summary>
public sealed record SearchEvaluationCaseResult(
    string QueryId,
    double RecallAtK,
    double MeanReciprocalRank,
    double NdcgAtK,
    IReadOnlyList<string> RankedBookIds);

/// <summary>Aggregate versioned search-retrieval metrics.</summary>
public sealed record SearchEvaluationReport(
    string EvaluationVersion,
    int CaseCount,
    double RecallAtK,
    double MeanReciprocalRank,
    double NdcgAtK,
    IReadOnlyList<SearchEvaluationCaseResult> Cases);
