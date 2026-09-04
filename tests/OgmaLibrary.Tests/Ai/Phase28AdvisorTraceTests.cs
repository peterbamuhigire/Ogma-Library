using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 28 durable advisor intent and candidate trace tests.</summary>
public sealed class Phase28AdvisorTraceTests
{
    [Fact]
    public async Task RecommendationPipeline_PersistsVersionedTraceWithoutRawQuery()
    {
        var audit = new InMemoryAuditRepository();
        BookMetadataDto candidate = new(
            "BOOK-P28-001",
            "Thinking in Systems",
            ["Donella Meadows"],
            ["systems"],
            [],
            "Systems description",
            null,
            2008,
            [],
            null);
        var pipeline = new RecommendationPipeline(
            new StubCatalogueReader([candidate]),
            new MetadataPayloadEnricher(),
            new StubGateway(),
            new RecommendationResponseParser(),
            new RecommendationProvenanceValidator(),
            new RecommendationStructuralValidator(),
            new StubHybridRanker(),
            new HybridRecommendationMerger(),
            audit);

        await pipeline.GetRecommendationsAsync(
            new RecommendationQuery("recommend systems books"),
            new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "test-model"),
            CancellationToken.None);

        AuditEvent trace = Assert.Single(audit.Events);
        Assert.Equal("AdvisorExecutionTrace", trace.EventType);
        Assert.DoesNotContain("recommend systems books", trace.Payload, StringComparison.Ordinal);
        using JsonDocument payload = JsonDocument.Parse(trace.Payload!);
        Assert.Equal("advisor-trace-v1", payload.RootElement.GetProperty("traceVersion").GetString());
        Assert.Equal("provider-success", payload.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("candidateCount").GetInt32());
        Assert.Equal("BOOK-P28-001", payload.RootElement.GetProperty("candidateBookIds")[0].GetString());
        Assert.Equal("advisor-intent-v1", payload.RootElement.GetProperty("intent").GetProperty("version").GetString());
        JsonElement stageCounts = payload.RootElement.GetProperty("stageCounts");
        Assert.Equal(1, stageCounts.GetProperty("catalogue").GetInt32());
        Assert.Equal(1, stageCounts.GetProperty("payload").GetInt32());
        Assert.Equal(1, stageCounts.GetProperty("provider").GetInt32());
        Assert.Equal(1, stageCounts.GetProperty("validated").GetInt32());
        Assert.Equal(1, stageCounts.GetProperty("final").GetInt32());
    }

    private sealed class StubCatalogueReader(IReadOnlyList<BookMetadataDto> candidates) : IAdvisorCatalogueReader
    {
        public Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(RecommendationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class StubGateway : IAiGateway
    {
        public Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletion("""
                [{"book_id":"BOOK-P28-001","rank":1,"confidence":0.9,"explanation":"Matches the systems topic.","provenance":[{"book_id":"BOOK-P28-001","field":"Tags","field_value":"systems"}]}]
                """));
    }

    private sealed class StubHybridRanker : IHybridRankerConsumer
    {
        public Task<IReadOnlyList<RankedCandidate>> RankAsync(RecommendationQuery query, IReadOnlyList<BookMetadataDto> candidates, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RankedCandidate>>([]);
    }

    private sealed class InMemoryAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> ReadRecentAsync(int maxCount, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>(Events.Take(maxCount).ToArray());
    }
}
