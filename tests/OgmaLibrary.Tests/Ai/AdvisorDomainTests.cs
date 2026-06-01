using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 advisor domain invariant tests.</summary>
public sealed class AdvisorDomainTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void ConfidenceScore_InvalidValue_Throws(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfidenceScore(value));
    }

    [Theory]
    [InlineData(0.0, ConfidenceLabel.Low)]
    [InlineData(0.49, ConfidenceLabel.Low)]
    [InlineData(0.5, ConfidenceLabel.Medium)]
    [InlineData(0.74, ConfidenceLabel.Medium)]
    [InlineData(0.75, ConfidenceLabel.High)]
    [InlineData(0.89, ConfidenceLabel.High)]
    [InlineData(0.9, ConfidenceLabel.VeryHigh)]
    [InlineData(1.0, ConfidenceLabel.VeryHigh)]
    public void ConfidenceScore_Label_UsesAdvisorBands(double value, ConfidenceLabel expected)
    {
        var score = new ConfidenceScore(value);

        Assert.Equal(expected, score.Label);
    }

    [Fact]
    public void RecommendationCard_Invariants_RequireOneBasedRankAndExplanation()
    {
        RecommendationExplanation explanation = CreateExplanation();

        RecommendationCard card = new(
            new BookId("BOOK-P13-0001"),
            1,
            new ConfidenceScore(0.83),
            explanation);

        Assert.Equal(1, card.Rank);
        Assert.Equal(ConfidenceLabel.High, card.Confidence.Label);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecommendationCard(new BookId("BOOK-P13-0001"), 0, new ConfidenceScore(0.5), explanation));
        Assert.Throws<ArgumentException>(() =>
            new RecommendationExplanation("Summary", [], "gpt-test", AiPrivacyTier.MetadataOnly));
    }

    [Fact]
    public void ReadingPlan_EmptySteps_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReadingPlan("Understand computer science", [], []));
    }

    [Fact]
    public void ReadingPlan_StructuralOracle_AcceptsValidPlan()
    {
        ReadingPlan plan = new(
            "Understand machine learning fundamentals",
            [
                new ReadingPlanStep(
                    new BookId("BOOK-P13-0001"),
                    "Start with definitions and core concepts.",
                    DifficultyLabel.Introductory,
                    5),
            ],
            [
                new Checkpoint(0, "Explain supervised and unsupervised learning in your own words."),
            ]);

        Assert.Single(plan.Steps);
        Assert.Single(plan.Checkpoints);
        Assert.Equal(DifficultyLabel.Introductory, plan.Steps[0].Difficulty);
    }

    [Fact]
    public void AnswerCitation_RequiresLocalEvidence()
    {
        AnswerCitation citation = new(
            new BookId("BOOK-P13-0001"),
            12,
            "chunk-12-0",
            "A short local evidence excerpt.",
            new ConfidenceScore(0.91));

        Assert.Equal(ConfidenceLabel.VeryHigh, citation.Confidence.Label);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnswerCitation(new BookId("BOOK-P13-0001"), 0, null, "Evidence", new ConfidenceScore(0.5)));
        Assert.Throws<ArgumentException>(() =>
            new AnswerCitation(new BookId("BOOK-P13-0001"), null, null, "", new ConfidenceScore(0.5)));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void AdvisorLabels_AreLocalized(string culture)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);

        Assert.DoesNotContain("Ai.Advisor.Confidence.VeryHigh", localization["Ai.Advisor.Confidence.VeryHigh"], StringComparison.Ordinal);
        Assert.DoesNotContain("Ai.Advisor.Difficulty.Introductory", localization["Ai.Advisor.Difficulty.Introductory"], StringComparison.Ordinal);
        Assert.DoesNotContain("Ai.Advisor.Difficulty.Expert", localization["Ai.Advisor.Difficulty.Expert"], StringComparison.Ordinal);
    }

    private static RecommendationExplanation CreateExplanation() =>
        new(
            "Matches your interest in systems thinking.",
            [
                new ProvenanceItem(
                    new BookId("BOOK-P13-0001"),
                    RecommendationMatchField.Tags,
                    "systems"),
            ],
            "gpt-test",
            AiPrivacyTier.MetadataOnly);
}
