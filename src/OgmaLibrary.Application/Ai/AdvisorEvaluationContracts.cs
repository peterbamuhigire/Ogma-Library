namespace OgmaLibrary.Application.Ai;

/// <summary>Versioned offline advisor relevance case.</summary>
public sealed record AdvisorEvaluationCase
{
    /// <summary>Creates an evaluation case with a bounded top-K target.</summary>
    public AdvisorEvaluationCase(
        string queryId,
        string queryText,
        IReadOnlyList<BookMetadataDto> candidates,
        IReadOnlySet<string> expectedHighlyRelevantBookIds,
        IReadOnlySet<string>? acceptableBookIds = null,
        int k = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(expectedHighlyRelevantBookIds);
        if (k is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "Evaluation K must be between 1 and 25.");
        }

        QueryId = queryId;
        QueryText = queryText;
        Candidates = candidates;
        ExpectedHighlyRelevantBookIds = expectedHighlyRelevantBookIds;
        AcceptableBookIds = acceptableBookIds ?? new HashSet<string>(StringComparer.Ordinal);
        K = k;
    }

    /// <summary>Stable case identifier.</summary>
    public string QueryId { get; }

    /// <summary>Benchmark query text.</summary>
    public string QueryText { get; }

    /// <summary>Local catalogue snapshot available to the evaluator.</summary>
    public IReadOnlyList<BookMetadataDto> Candidates { get; }

    /// <summary>Highly relevant local IDs used for recall and ranking metrics.</summary>
    public IReadOnlySet<string> ExpectedHighlyRelevantBookIds { get; }

    /// <summary>Acceptable but not ideal local IDs.</summary>
    public IReadOnlySet<string> AcceptableBookIds { get; }

    /// <summary>Evaluation cutoff.</summary>
    public int K { get; }
}

/// <summary>Metrics for one offline advisor evaluation case.</summary>
public sealed record AdvisorEvaluationCaseResult(
    string QueryId,
    double PrecisionAtK,
    double RecallAtK,
    double MeanReciprocalRank,
    double NdcgAtK,
    double GroundingRate,
    double ConstraintSatisfactionRate,
    double DiversityRate,
    IReadOnlyList<string> RankedBookIds);

/// <summary>Aggregate offline advisor metrics across a labeled evaluation set.</summary>
public sealed record AdvisorEvaluationReport(
    string EvaluationVersion,
    int CaseCount,
    double PrecisionAtK,
    double RecallAtK,
    double MeanReciprocalRank,
    double NdcgAtK,
    double GroundingRate,
    double ConstraintSatisfactionRate,
    double DiversityRate,
    IReadOnlyList<AdvisorEvaluationCaseResult> Cases);

/// <summary>Approved lower bounds for a human-labeled offline evaluation set.</summary>
public sealed record AdvisorEvaluationThresholds(
    double PrecisionAtK,
    double RecallAtK,
    double MeanReciprocalRank,
    double NdcgAtK,
    double GroundingRate,
    double ConstraintSatisfactionRate,
    double DiversityRate)
{
    /// <summary>Validates all metric bounds are percentages in the unit interval.</summary>
    public void Validate()
    {
        double[] values = [
            PrecisionAtK,
            RecallAtK,
            MeanReciprocalRank,
            NdcgAtK,
            GroundingRate,
            ConstraintSatisfactionRate,
            DiversityRate,
        ];
        if (values.Any(value => double.IsNaN(value) || value is < 0 or > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(AdvisorEvaluationThresholds),
                "Evaluation thresholds must be between 0 and 1.");
        }
    }
}

/// <summary>Fail-closed outcome of comparing an offline report to thresholds.</summary>
public sealed record AdvisorEvaluationGateResult(
    AdvisorEvaluationReport Report,
    bool Passed,
    IReadOnlyList<string> FailedMetrics);
