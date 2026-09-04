using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Durability and path-safety coverage for Phase 26 evaluation runs.</summary>
public sealed class SearchEvaluationStoreTests
{
    [Fact]
    public async Task Store_RoundTripsJudgmentsAndReport_AndDeletesAtomicallyReplacedRun()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ogma-evaluation-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSearchEvaluationStore(directory);
            var evaluationCase = new SearchEvaluationCase(
                "case-1", "education", ["book-1", "book-2"],
                new HashSet<string>(["book-2"], StringComparer.Ordinal), 2);
            SearchEvaluationReport report = SearchOfflineEvaluator.Evaluate([evaluationCase]);
            var run = new SearchEvaluationRun("run-2026-09-04", DateTimeOffset.UtcNow, [evaluationCase], report);

            await store.SaveAsync(run);
            SearchEvaluationRun loaded = (await store.GetAsync(run.RunId))!;

            Assert.Equal(run.RunId, loaded.RunId);
            Assert.Equal(run.Report.EvaluationVersion, loaded.Report.EvaluationVersion);
            Assert.Equal(run.Report.CaseCount, loaded.Report.CaseCount);
            Assert.Equal(run.Report.RecallAtK, loaded.Report.RecallAtK);
            Assert.Equal(run.Report.MeanReciprocalRank, loaded.Report.MeanReciprocalRank);
            Assert.Equal(run.Report.NdcgAtK, loaded.Report.NdcgAtK);
            Assert.Equal(run.Report.Cases[0].RankedBookIds, loaded.Report.Cases[0].RankedBookIds);
            Assert.Equal(run.Cases[0].RelevantBookIds, loaded.Cases[0].RelevantBookIds);
            Assert.Single(Directory.GetFiles(directory, "*.json"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
            Assert.True(await store.DeleteAsync(run.RunId));
            Assert.False(await store.DeleteAsync(run.RunId));
            Assert.Null(await store.GetAsync(run.RunId));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Store_RejectsPathTraversalRunIds()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ogma-evaluation-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSearchEvaluationStore(directory);
            await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync("../outside"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
