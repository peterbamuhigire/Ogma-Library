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

/// <summary>Durable local evaluation run containing judgments and its report.</summary>
public sealed record SearchEvaluationRun(
    string RunId,
    DateTimeOffset CapturedUtc,
    IReadOnlyList<SearchEvaluationCase> Cases,
    SearchEvaluationReport Report);

/// <summary>Persists versioned local search evaluation runs outside catalogue data.</summary>
public interface ISearchEvaluationStore
{
    /// <summary>Atomically creates or replaces one evaluation run.</summary>
    Task SaveAsync(SearchEvaluationRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads an evaluation run, or null when it does not exist.</summary>
    Task<SearchEvaluationRun?> GetAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an evaluation run and reports whether it existed.</summary>
    Task<bool> DeleteAsync(string runId, CancellationToken cancellationToken = default);
}
