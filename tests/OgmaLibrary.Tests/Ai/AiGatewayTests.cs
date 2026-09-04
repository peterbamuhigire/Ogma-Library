using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 AI gateway core tests.</summary>
public sealed class AiGatewayTests
{
    [Fact]
    public async Task AiGateway_Tier0_ThrowsWithoutProviderCall()
    {
        var provider = new FakeProvider();
        var audit = new FakeAuditRepository();
        AiGateway gateway = CreateGateway(provider, activeTier: AiPrivacyTier.Offline, audit: audit);

        await Assert.ThrowsAsync<AiDisabledException>(() =>
            gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(0, provider.CallCount);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task AiDisabledProvider_AlwaysThrowsDisabled()
    {
        var provider = new AiDisabledProvider();

        await Assert.ThrowsAsync<AiDisabledException>(() =>
            provider.CompleteAsync(CreateRequest(), CancellationToken.None));

        Assert.True(provider.IsLocalOnly);
        Assert.Equal("disabled", provider.ProviderKey);
    }

    [Fact]
    public async Task AiGateway_RequiresConsentBeforeProviderCall()
    {
        var provider = new FakeProvider();
        var privacy = new FakePrivacyService(AiPrivacyTier.MetadataOnly) { HasConsent = false };
        var audit = new FakeAuditRepository();
        AiGateway gateway = CreateGateway(provider, privacy: privacy, audit: audit);

        await Assert.ThrowsAsync<AiConsentRequiredException>(() =>
            gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(0, provider.CallCount);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task AiGateway_PreviewCancel_PreventsProviderAndAudit()
    {
        var provider = new FakeProvider();
        var preview = new FakePreviewGate { Decision = AiPreviewDecision.Cancel };
        var audit = new FakeAuditRepository();
        AiGateway gateway = CreateGateway(provider, preview: preview, audit: audit);

        await Assert.ThrowsAsync<AiPreviewCancelledException>(() =>
            gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(1, preview.CallCount);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task AiGateway_WritesAuditAndHistoryOnSuccess()
    {
        var provider = new FakeProvider
        {
            Completion = new AiCompletion("Use the shelf for daily reading.", 1000, 500, 100),
        };
        var audit = new FakeAuditRepository();
        var history = new FakeHistoryRepository();
        var costs = new AiCostCalculator(
        [
            new AiModelPrice("openai", "gpt-test", 2m, 4m),
        ]);
        AiGateway gateway = CreateGateway(provider, audit: audit, history: history, costs: costs);

        AiCompletion completion = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("Use the shelf for daily reading.", completion.Text);
        Assert.Single(history.Entries);
        AiAuditEvent auditEvent = Assert.Single(audit.Events);
        Assert.Equal(AiPrivacyTier.MetadataOnly, auditEvent.Tier);
        Assert.Equal("openai", auditEvent.Provider);
        Assert.Equal("gpt-test", auditEvent.Model);
        Assert.Equal(64, auditEvent.PayloadHash.Length);
        Assert.Equal(64, auditEvent.ResponseHash.Length);
        Assert.Equal(history.Entries[0].Id, auditEvent.QueryHistoryEntryId);
        Assert.Equal(0.004m, auditEvent.EstimatedCostUsd);
    }

    [Fact]
    public async Task AiGateway_WritesAuditWhenProviderFails()
    {
        var provider = new FakeProvider { Failure = new InvalidOperationException("provider failed") };
        var audit = new FakeAuditRepository();
        var history = new FakeHistoryRepository();
        AiGateway gateway = CreateGateway(provider, audit: audit, history: history);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Empty(history.Entries);
        AiAuditEvent auditEvent = Assert.Single(audit.Events);
        Assert.Equal(64, auditEvent.PayloadHash.Length);
        Assert.Equal(64, auditEvent.ResponseHash.Length);
        Assert.Null(auditEvent.QueryHistoryEntryId);
    }

    [Fact]
    public async Task AiGateway_RejectsProviderMismatchBeforeEgress()
    {
        var provider = new FakeProvider();
        AiGateway gateway = CreateGateway(provider);
        AiRequest request = new(
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "claude-test",
            "recommendation",
            "recommend");

        await Assert.ThrowsAsync<AiTierViolationException>(() =>
            gateway.SendAsync(request, CancellationToken.None));

        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void AiPayloadBuilder_HashChangesWhenPayloadChanges()
    {
        var builder = new AiPayloadBuilder();
        AiPayloadPreview first = builder.BuildPreview(CreateRequest(query: "recommend"));
        AiPayloadPreview second = builder.BuildPreview(CreateRequest(query: "recommend another"));

        Assert.NotEqual(builder.ComputePayloadHash(first), builder.ComputePayloadHash(second));
    }

    private static AiGateway CreateGateway(
        FakeProvider? provider = null,
        FakePrivacyService? privacy = null,
        FakePreviewGate? preview = null,
        FakeAuditRepository? audit = null,
        FakeHistoryRepository? history = null,
        IAiCostCalculator? costs = null,
        AiPrivacyTier activeTier = AiPrivacyTier.MetadataOnly) =>
        new(
            provider ?? new FakeProvider(),
            privacy ?? new FakePrivacyService(activeTier),
            new AiPayloadBuilder(),
            preview ?? new FakePreviewGate(),
            audit ?? new FakeAuditRepository(),
            history ?? new FakeHistoryRepository(),
            costs ?? new AiCostCalculator());

    private static AiRequest CreateRequest(string query = "recommend") =>
        new(
            AiPrivacyTier.MetadataOnly,
            "openai",
            "gpt-test",
            "recommendation",
            query,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["author"] = "Chwezi Core Systems",
                ["title"] = "Ogma Library",
            });

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderKey => "openai";

        public bool IsLocalOnly { get; init; }

        public int CallCount { get; private set; }

        public AiCompletion Completion { get; init; } = new("ok", 10, 5);

        public Exception? Failure { get; init; }

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Completion);
        }
    }

    private sealed class FakePrivacyService(AiPrivacyTier activeTier) : IAiPrivacyService
    {
        public bool HasConsent { get; init; } = true;

        public AiPrivacyTier GetActiveTier() => activeTier;

        public Task SetTierAsync(AiPrivacyTier tier, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordConsentAsync(AiConsentRecord consent, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> HasConsentAsync(
            AiPrivacyTier tier,
            string provider,
            string scope,
            CancellationToken cancellationToken) =>
            Task.FromResult(HasConsent);

        public AiPayloadPreview BuildPayloadPreview(AiRequest request) =>
            new AiPayloadBuilder().BuildPreview(request);
    }

    private sealed class FakePreviewGate : IAiPreviewGate
    {
        public AiPreviewDecision Decision { get; init; } = AiPreviewDecision.Send;

        public int CallCount { get; private set; }

        public Task<AiPreviewDecision> ShowAsync(AiPayloadPreview preview, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Decision);
        }
    }

    private sealed class FakeAuditRepository : IAiAuditRepository
    {
        public List<AiAuditEvent> Events { get; } = [];

        public Task AppendAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AiAuditEvent>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiAuditEvent>>(Events.Take(count).ToList());

        public Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeHistoryRepository : IAiQueryHistoryRepository
    {
        public List<AiQueryHistoryEntry> Entries { get; } = [];

        public Task AddAsync(AiQueryHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AiQueryHistoryEntry>> ListAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiQueryHistoryEntry>>(Entries.Skip(page * pageSize).Take(pageSize).ToList());

        public Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Entries.Any(entry => entry.Id == id));

        public Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<int> HardDeleteAllAsync(CancellationToken cancellationToken)
        {
            int count = Entries.Count;
            Entries.Clear();
            return Task.FromResult(count);
        }
    }
}
