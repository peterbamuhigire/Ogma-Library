using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 26 deterministic retrieval-evaluation contract tests.</summary>
public sealed class Phase26SearchEvaluationTests
{
    [Fact]
    public void Evaluator_ComputesRecallMrrAndNdcgAtBoundedCutoff()
    {
        var evaluationCase = new SearchEvaluationCase(
            "P26-EVAL-001",
            "distributed systems",
            ["BOOK-A", "BOOK-C", "BOOK-B", "BOOK-D"],
            new HashSet<string>(["BOOK-B", "BOOK-C"], StringComparer.Ordinal),
            k: 3);

        SearchEvaluationReport report = SearchOfflineEvaluator.Evaluate([evaluationCase]);

        SearchEvaluationCaseResult result = Assert.Single(report.Cases);
        Assert.Equal("search-retrieval-evaluation-v1", report.EvaluationVersion);
        Assert.Equal(1, report.CaseCount);
        Assert.Equal(1.0, result.RecallAtK);
        Assert.Equal(0.5, result.MeanReciprocalRank);
        Assert.InRange(result.NdcgAtK, 0.69, 0.70);
        Assert.Equal(["BOOK-A", "BOOK-C", "BOOK-B"], result.RankedBookIds);
    }

    [Fact]
    public void Evaluator_UsesStableEmptyJudgmentConventions()
    {
        var evaluationCase = new SearchEvaluationCase(
            "P26-EVAL-002",
            "unjudged query",
            ["BOOK-A", "BOOK-B"],
            new HashSet<string>(StringComparer.Ordinal),
            k: 1);

        SearchEvaluationReport report = SearchOfflineEvaluator.Evaluate([evaluationCase]);

        SearchEvaluationCaseResult result = Assert.Single(report.Cases);
        Assert.Equal(1.0, result.RecallAtK);
        Assert.Equal(0.0, result.MeanReciprocalRank);
        Assert.Equal(1.0, result.NdcgAtK);
        Assert.Equal(["BOOK-A"], result.RankedBookIds);
    }

    [Fact]
    public void EvaluationCase_RejectsUnboundedCutoff()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SearchEvaluationCase(
            "P26-EVAL-003",
            "query",
            [],
            new HashSet<string>(StringComparer.Ordinal),
            k: 101));
    }
}
