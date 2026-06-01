using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 structured reading-plan pipeline tests.</summary>
public sealed class ReadingPlanPipelineTests
{
    [Fact]
    public async Task ReadingPlan_StructuralOracle()
    {
        BookMetadataDto[] candidates =
        [
            Candidate("BOOK-P13-PLAN-001", "Machine Learning Basics"),
            Candidate("BOOK-P13-PLAN-002", "Practical Model Evaluation"),
        ];
        var gateway = new QueueAiGateway(
            """
            {
              "goal": "understand machine learning fundamentals",
              "steps": [
                {
                  "book_id": "BOOK-P13-PLAN-001",
                  "rationale": "Start with vocabulary and core concepts.",
                  "difficulty": "Introductory",
                  "estimated_reading_days": 5
                },
                {
                  "book_id": "BOOK-P13-PLAN-002",
                  "rationale": "Then learn how to evaluate models.",
                  "difficulty": "Intermediate",
                  "estimated_reading_days": 7
                }
              ],
              "checkpoints": [
                { "after_step": 0, "description": "Explain supervised learning in plain language." }
              ]
            }
            """);
        ReadingPlanPipeline pipeline = CreatePipeline(candidates, gateway);

        ReadingPlan plan = await pipeline.GetReadingPlanAsync(
            new ReadingPlanRequest("understand machine learning fundamentals", maxBooks: 2),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "anthropic", "claude-test"),
            CancellationToken.None);

        Assert.Equal("understand machine learning fundamentals", plan.Goal);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Single(plan.Checkpoints);
        Assert.All(plan.Steps, step => Assert.Contains(candidates, candidate => candidate.BookId == step.BookId.Value));
        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("reading-plan", gateway.LastRequest!.QueryType);
        Assert.Contains("prompt.template", gateway.LastRequest.MetadataFields.Keys);
        Assert.Empty(gateway.LastRequest.ContentChunks);
    }

    [Fact]
    public async Task ReadingPlanParser_Retry_OnParseFailure()
    {
        BookMetadataDto[] candidates = [Candidate("BOOK-P13-PLAN-001", "Machine Learning Basics")];
        var gateway = new QueueAiGateway(
            "{ malformed json",
            """
            {
              "goal": "learn machine learning",
              "steps": [
                {
                  "book_id": "BOOK-P13-PLAN-001",
                  "rationale": "Start with the local beginner text.",
                  "difficulty": "Introductory",
                  "estimated_reading_days": 4
                }
              ],
              "checkpoints": []
            }
            """);
        ReadingPlanPipeline pipeline = CreatePipeline(candidates, gateway);

        ReadingPlan plan = await pipeline.GetReadingPlanAsync(
            new ReadingPlanRequest("learn machine learning"),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "anthropic", "claude-test"),
            CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal(2, gateway.SendCount);
    }

    [Fact]
    public void ReadingPlanParser_RejectsNonLocalBookId()
    {
        ReadingPlanParser parser = new();

        Assert.Throws<AdvisorParseException>(() =>
            parser.Parse(
                """
                {
                  "goal": "learn machine learning",
                  "steps": [
                    {
                      "book_id": "BOOK-P13-NOT-LOCAL",
                      "rationale": "Bad id.",
                      "difficulty": "Introductory"
                    }
                  ],
                  "checkpoints": []
                }
                """,
                [Candidate("BOOK-P13-PLAN-001", "Machine Learning Basics")]));
    }

    [Fact]
    public void ReadingPlanPromptTemplate_LoadsEmbeddedSchema()
    {
        string prompt = ReadingPlanPromptTemplate.Load();

        Assert.Contains("\"steps\"", prompt, StringComparison.Ordinal);
        Assert.Contains("Never invent a book_id", prompt, StringComparison.Ordinal);
    }

    private static ReadingPlanPipeline CreatePipeline(
        IReadOnlyList<BookMetadataDto> candidates,
        IAiGateway gateway) =>
        new(
            new FakeCatalogueReader(candidates),
            new MetadataPayloadEnricher(),
            gateway,
            new ReadingPlanParser());

    private static BookMetadataDto Candidate(string bookId, string title) =>
        new(
            bookId,
            title,
            ["Chwezi Core Systems"],
            ["learning"],
            ["Education"],
            $"Description for {title}.",
            null,
            2026,
            [],
            null);

    private sealed class FakeCatalogueReader(IReadOnlyList<BookMetadataDto> candidates) : IAdvisorCatalogueReader
    {
        public Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
            RecommendationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class QueueAiGateway(params string[] responses) : IAiGateway
    {
        private readonly Queue<string> _responses = new(responses);

        public AiRequest? LastRequest { get; private set; }

        public int SendCount { get; private set; }

        public Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            SendCount++;
            return Task.FromResult(new AiCompletion(_responses.Dequeue(), 100, 50));
        }
    }
}
