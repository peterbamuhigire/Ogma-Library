using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>Calculates deterministic offline retrieval metrics without provider calls.</summary>
public static class SearchOfflineEvaluator
{
    /// <summary>Stable version of the search evaluation metric contract.</summary>
    public const string EvaluationVersion = SearchContractVersions.Evaluation;

    /// <summary>Evaluates captured ranked results against local relevance judgments.</summary>
    public static SearchEvaluationReport Evaluate(IReadOnlyList<SearchEvaluationCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        List<SearchEvaluationCaseResult> results = cases.Select(Measure).ToList();
        return new SearchEvaluationReport(
            EvaluationVersion,
            results.Count,
            Average(results, result => result.RecallAtK),
            Average(results, result => result.MeanReciprocalRank),
            Average(results, result => result.NdcgAtK),
            results);
    }

    private static SearchEvaluationCaseResult Measure(SearchEvaluationCase evaluationCase)
    {
        IReadOnlyList<string> ranked = evaluationCase.RankedBookIds
            .Take(evaluationCase.K)
            .ToArray();
        int relevantRetrieved = ranked
            .Distinct(StringComparer.Ordinal)
            .Count(evaluationCase.RelevantBookIds.Contains);
        double recall = evaluationCase.RelevantBookIds.Count == 0
            ? 1.0
            : relevantRetrieved / (double)evaluationCase.RelevantBookIds.Count;
        int firstRelevantRank = ranked
            .Select((bookId, index) => (bookId, index))
            .Where(item => evaluationCase.RelevantBookIds.Contains(item.bookId))
            .Select(item => item.index + 1)
            .FirstOrDefault();
        double reciprocalRank = firstRelevantRank == 0 ? 0.0 : 1.0 / firstRelevantRank;

        double dcg = ranked
            .Select((bookId, index) =>
                evaluationCase.RelevantBookIds.Contains(bookId)
                    ? 1.0 / Math.Log2(index + 2.0)
                    : 0.0)
            .Sum();
        double idealDcg = Enumerable.Range(0, Math.Min(evaluationCase.K, evaluationCase.RelevantBookIds.Count))
            .Sum(index => 1.0 / Math.Log2(index + 2.0));
        double ndcg = idealDcg == 0.0 ? 1.0 : dcg / idealDcg;

        return new SearchEvaluationCaseResult(
            evaluationCase.QueryId,
            Math.Clamp(recall, 0.0, 1.0),
            Math.Clamp(reciprocalRank, 0.0, 1.0),
            Math.Clamp(ndcg, 0.0, 1.0),
            ranked);
    }

    private static double Average(
        List<SearchEvaluationCaseResult> results,
        Func<SearchEvaluationCaseResult, double> selector) =>
        results.Count == 0 ? 0.0 : results.Average(selector);
}
