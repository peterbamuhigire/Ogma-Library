using System.Globalization;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 13: aggregate relevance metrics for the oracle battery over the synthetic
/// corpus (precision@1, precision@3 of the shown results, recall@3, recall@10 and MRR), so the
/// completion record reports measured numbers rather than a pass count. The gate is the oracle
/// expectation: every positive query has its book first, and the nonsense word finds nothing.
/// </summary>
public sealed class SearchRelevanceMetricsTests : IClassFixture<SearchRelevanceOracleTests.CorpusFixture>
{
    private readonly SearchRelevanceOracleTests.CorpusFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SearchRelevanceMetricsTests(SearchRelevanceOracleTests.CorpusFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>Positive judgments: the oracle top-one cases plus the body-words query.</summary>
    public static IReadOnlyList<(string Query, string Relevant)> Judgments { get; } =
        SearchRelevanceOracleTests.TopOneCases
            .Select(row => ((string)row[0], (string)row[1]))
            .Append(("amber library lantern", "The Lantern Keeper"))
            .ToList();

    [Fact]
    public async Task Oracle_Metrics_MeetTheGate()
    {
        OracleMetrics metrics = await OracleMetrics.MeasureAsync(Judgments, "zzqxnotaword", _fixture.SearchTitlesAsync);

        _output.WriteLine(metrics.ToString());
        foreach (string line in metrics.PerQuery)
        {
            _output.WriteLine(line);
        }

        Assert.Equal(1.0, metrics.PrecisionAt1);
        Assert.Equal(1.0, metrics.RecallAt3);
        Assert.True(metrics.NegativeEmpty, "The nonsense query must return no results.");
    }

    /// <summary>Aggregate metrics for one pipeline over the judged queries.</summary>
    internal sealed record OracleMetrics(
        int Queries,
        double PrecisionAt1,
        double PrecisionAt3Shown,
        double RecallAt3,
        double RecallAt10,
        double MeanReciprocalRank,
        bool NegativeEmpty,
        IReadOnlyList<string> PerQuery)
    {
        /// <summary>Runs every judged query through <paramref name="search"/> and aggregates.</summary>
        public static async Task<OracleMetrics> MeasureAsync(
            IReadOnlyList<(string Query, string Relevant)> judgments,
            string negativeQuery,
            Func<string, Task<IReadOnlyList<string>>> search)
        {
            double p1 = 0, p3 = 0, r3 = 0, r10 = 0, mrr = 0;
            var perQuery = new List<string>();
            foreach ((string query, string relevant) in judgments)
            {
                IReadOnlyList<string> titles = await search(query);
                int rank = titles.ToList().FindIndex(title => title == relevant) + 1;
                List<string> top3 = titles.Take(3).ToList();
                p1 += rank == 1 ? 1 : 0;
                p3 += top3.Count == 0 ? 0 : top3.Count(title => title == relevant) / (double)top3.Count;
                r3 += rank is > 0 and <= 3 ? 1 : 0;
                r10 += rank is > 0 and <= 10 ? 1 : 0;
                mrr += rank > 0 ? 1.0 / rank : 0;
                perQuery.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  '{query}' rank={rank} returned={titles.Count} top=[{string.Join(" | ", top3)}]"));
            }

            IReadOnlyList<string> negative = await search(negativeQuery);
            int n = judgments.Count;
            return new OracleMetrics(n, p1 / n, p3 / n, r3 / n, r10 / n, mrr / n, negative.Count == 0, perQuery);
        }

        /// <inheritdoc />
        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"ORACLE-METRICS queries={Queries} P@1={PrecisionAt1:0.000} P@3(shown)={PrecisionAt3Shown:0.000} " +
            $"R@3={RecallAt3:0.000} R@10={RecallAt10:0.000} MRR={MeanReciprocalRank:0.000} negativeEmpty={NegativeEmpty}");
    }
}
