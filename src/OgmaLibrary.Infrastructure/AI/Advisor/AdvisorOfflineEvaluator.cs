using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Runs deterministic advisor relevance evaluation without provider calls.</summary>
public sealed class AdvisorOfflineEvaluator
{
    /// <summary>Stable version of the evaluator metric contract.</summary>
    public const string EvaluationVersion = "advisor-evaluation-v1";

    /// <summary>Evaluates labeled local cases using the deterministic candidate ranker.</summary>
    public static AdvisorEvaluationReport Evaluate(IReadOnlyList<AdvisorEvaluationCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        List<AdvisorEvaluationCaseResult> results = [];
        foreach (AdvisorEvaluationCase evaluationCase in cases)
        {
            AdvisorIntent intent = AdvisorIntentParser.Parse(evaluationCase.QueryText);
            IReadOnlyList<BookMetadataDto> ranked = AdvisorCandidateRanker.Rank(
                evaluationCase.Candidates,
                intent,
                Math.Min(50, Math.Max(evaluationCase.K, evaluationCase.Candidates.Count)))
                .Take(evaluationCase.K)
                .ToArray();
            results.Add(Measure(evaluationCase, ranked, intent));
        }

        return new AdvisorEvaluationReport(
            EvaluationVersion,
            results.Count,
            Average(results, result => result.PrecisionAtK),
            Average(results, result => result.RecallAtK),
            Average(results, result => result.MeanReciprocalRank),
            Average(results, result => result.NdcgAtK),
            Average(results, result => result.GroundingRate),
            Average(results, result => result.ConstraintSatisfactionRate),
            Average(results, result => result.DiversityRate),
            results);
    }

    private static AdvisorEvaluationCaseResult Measure(
        AdvisorEvaluationCase evaluationCase,
        IReadOnlyList<BookMetadataDto> ranked,
        AdvisorIntent intent)
    {
        int relevantCount = ranked.Count(candidate => evaluationCase.ExpectedHighlyRelevantBookIds.Contains(candidate.BookId));
        double precision = relevantCount / (double)evaluationCase.K;
        double recall = evaluationCase.ExpectedHighlyRelevantBookIds.Count == 0
            ? 1.0
            : relevantCount / (double)evaluationCase.ExpectedHighlyRelevantBookIds.Count;
        int firstRelevant = ranked
            .Select((candidate, index) => (candidate, index))
            .Where(item => evaluationCase.ExpectedHighlyRelevantBookIds.Contains(item.candidate.BookId))
            .Select(item => item.index + 1)
            .FirstOrDefault();
        double reciprocalRank = firstRelevant == 0 ? 0.0 : 1.0 / firstRelevant;

        double dcg = ranked
            .Select((candidate, index) => Relevance(evaluationCase, candidate) / Math.Log2(index + 2.0))
            .Sum();
        double idealDcg = evaluationCase.ExpectedHighlyRelevantBookIds
            .Select((_, index) => 2.0 / Math.Log2(index + 2.0))
            .Take(evaluationCase.K)
            .Sum();
        double ndcg = idealDcg == 0.0 ? 1.0 : dcg / idealDcg;
        double grounding = ranked.Count == 0
            ? 1.0
            : ranked.Count(candidate => evaluationCase.Candidates.Any(local => local.BookId == candidate.BookId)) / (double)ranked.Count;
        double constraints = ranked.Count == 0
            ? 1.0
            : ranked.Count(candidate => !HasNegativeMatch(candidate, intent.NegativeTerms)) / (double)ranked.Count;
        double diversity = ranked.Count == 0
            ? 1.0
            : ranked.Select(candidate => candidate.Authors.Count > 0 ? candidate.Authors[0] : candidate.BookId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() / (double)ranked.Count;

        return new AdvisorEvaluationCaseResult(
            evaluationCase.QueryId,
            Math.Clamp(precision, 0.0, 1.0),
            Math.Clamp(recall, 0.0, 1.0),
            Math.Clamp(reciprocalRank, 0.0, 1.0),
            Math.Clamp(ndcg, 0.0, 1.0),
            grounding,
            constraints,
            diversity,
            ranked.Select(candidate => candidate.BookId).ToArray());
    }

    private static double Relevance(AdvisorEvaluationCase evaluationCase, BookMetadataDto candidate) =>
        evaluationCase.ExpectedHighlyRelevantBookIds.Contains(candidate.BookId)
            ? 2.0
            : evaluationCase.AcceptableBookIds.Contains(candidate.BookId) ? 1.0 : 0.0;

    private static bool HasNegativeMatch(BookMetadataDto candidate, IReadOnlyList<string> negativeTerms) =>
        negativeTerms.Any(term => string.Join(' ', candidate.Title, candidate.Authors, candidate.Tags, candidate.Categories, candidate.Description, candidate.Notes)
            .Contains(term, StringComparison.OrdinalIgnoreCase));

    private static double Average(
        List<AdvisorEvaluationCaseResult> results,
        Func<AdvisorEvaluationCaseResult, double> selector) =>
        results.Count == 0 ? 0.0 : results.Average(selector);
}
