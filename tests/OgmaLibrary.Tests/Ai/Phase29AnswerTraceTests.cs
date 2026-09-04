using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 29 durable answer-evidence trace tests.</summary>
public sealed class Phase29AnswerTraceTests
{
    [Fact]
    public async Task Answer_PersistsHashedValidationTraceWithoutQuestionOrExcerpt()
    {
        var audit = new RecordingAuditRepository();
        var pipeline = new LocalEvidenceAnswerPipeline(
            new FakeSemanticSearchService(
            [
                new SemanticSearchResult(
                    "book-29",
                    "Local book",
                    7,
                    SearchChunkSource.Page,
                    "Private local excerpt that must not enter the audit payload.",
                    0.91f,
                    false,
                    0.88,
                    PageIndex: 6),
            ]),
            audit);

        AnswerResponse response = await pipeline.GetAnswerAsync(
            new AnswerRequest("What is the private question?", 1, allowContentAwareTier: true),
            CancellationToken.None);

        Assert.Single(response.Citations);
        AuditEvent trace = Assert.Single(audit.Events);
        Assert.Equal("AnswerEvidenceTrace", trace.EventType);
        Assert.DoesNotContain("What is the private question?", trace.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Private local excerpt", trace.Payload, StringComparison.Ordinal);

        using JsonDocument payload = JsonDocument.Parse(trace.Payload!);
        Assert.Equal("answer-evidence-trace-v1", payload.RootElement.GetProperty("traceVersion").GetString());
        Assert.Equal("extractive-local-evidence", payload.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("citationCount").GetInt32());
        Assert.Equal("page", payload.RootElement.GetProperty("citations")[0].GetProperty("sourceLabel").GetString());
    }

    [Fact]
    public async Task Answer_WithNoEvidence_PersistsSafeOutcomeTrace()
    {
        var audit = new RecordingAuditRepository();
        var pipeline = new LocalEvidenceAnswerPipeline(new FakeSemanticSearchService([]), audit);

        AnswerResponse response = await pipeline.GetAnswerAsync(
            new AnswerRequest("Unknown local question"),
            CancellationToken.None);

        Assert.Empty(response.Citations);
        AuditEvent trace = Assert.Single(audit.Events);
        using JsonDocument payload = JsonDocument.Parse(trace.Payload!);
        Assert.Equal("no-local-evidence", payload.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(0, payload.RootElement.GetProperty("citationCount").GetInt32());
    }

    private sealed class FakeSemanticSearchService(IReadOnlyList<SemanticSearchResult> results) : ISemanticSearchService
    {
        public Task<SemanticSearchResponse> SearchAsync(string queryText, int maxResults, CancellationToken cancellationToken) =>
            Task.FromResult(new SemanticSearchResponse(false, false, results.Take(maxResults).ToArray()));
    }

    private sealed class RecordingAuditRepository : IAuditRepository
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
