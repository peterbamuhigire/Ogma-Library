using System.Text.Json;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 offline AI evaluation harness artifact tests.</summary>
public sealed class Phase13EvaluationHarnessTests
{
    [Fact]
    public void EvaluationQueries_ContainTwentyStructuralFixtures()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RepoPath("tests", "evaluation", "phase-13", "queries.json")));
        JsonElement queries = document.RootElement.GetProperty("queries");

        Assert.Equal(20, queries.GetArrayLength());
        foreach (JsonElement query in queries.EnumerateArray())
        {
            Assert.StartsWith("P13-EVAL-", query.GetProperty("id").GetString(), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(query.GetProperty("goal").GetString()));
            Assert.True(query.GetProperty("expectedSignals").GetArrayLength() >= 3);
        }
    }

    [Fact]
    public void EvaluationBenchmark_RecordsPassingMockStructuralRun()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RepoPath("docs", "benchmarks", "phase-13", "eval-mock-20260601.json")));
        JsonElement root = document.RootElement;

        Assert.Equal("13", root.GetProperty("phase").GetString());
        Assert.Equal("mock", root.GetProperty("providerMode").GetString());
        Assert.Equal(20, root.GetProperty("queryCount").GetInt32());
        Assert.Equal(1.0, root.GetProperty("structuralPassRate").GetDouble());
        Assert.Equal(20, root.GetProperty("results").GetArrayLength());
        Assert.True(root.GetProperty("confidenceDistribution").GetProperty("High").GetInt32() > 0);
        Assert.True(root.GetProperty("avgExplanationLength").GetDouble() >= 75.0);
    }

    private static string RepoPath(params string[] parts)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "OgmaLibrary.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine([current!.FullName, .. parts]);
    }
}
