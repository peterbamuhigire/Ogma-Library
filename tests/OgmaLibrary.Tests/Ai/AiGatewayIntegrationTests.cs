using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 end-to-end AI gateway integration tests over SQLite repositories.</summary>
public sealed class AiGatewayIntegrationTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public AiGatewayIntegrationTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task AiGateway_WithConsent_PersistsAuditAndErasableHistory()
    {
        var consents = new AiConsentRepository(_context);
        var audit = new AiAuditRepository(_context);
        var history = new AiQueryHistoryRepository(_context);
        var privacy = new AiPrivacyService(consents, new AiPayloadBuilder());
        await privacy.SetTierAsync(AiPrivacyTier.MetadataOnly, CancellationToken.None);
        await privacy.RecordConsentAsync(
            new AiConsentRecord(
                "consent-integration",
                AiPrivacyTier.MetadataOnly,
                "openai",
                "library:default",
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var preview = new CapturingPreviewGate();
        var gateway = new AiGateway(
            new FakeProvider(),
            privacy,
            new AiPayloadBuilder(),
            preview,
            audit,
            history,
            new AiCostCalculator([new AiModelPrice("openai", "gpt-test", 2m, 4m)]));

        AiCompletion completion = await gateway.SendAsync(CreateRequest(), CancellationToken.None);
        IReadOnlyList<AiAuditEvent> auditEvents = await audit.GetRecentAsync(10, CancellationToken.None);
        IReadOnlyList<AiQueryHistoryEntry> historyEntries = await history.ListAsync(0, 10, CancellationToken.None);
        int deletedHistory = await history.HardDeleteAllAsync(CancellationToken.None);
        IReadOnlyList<AiAuditEvent> auditAfterDelete = await audit.GetRecentAsync(10, CancellationToken.None);

        Assert.Equal("integration answer", completion.Text);
        Assert.Equal(1, preview.CallCount);
        AiAuditEvent auditEvent = Assert.Single(auditEvents);
        Assert.Equal(0.00004m, auditEvent.EstimatedCostUsd);
        AiQueryHistoryEntry historyEntry = Assert.Single(historyEntries);
        Assert.Equal(historyEntry.Id, auditEvent.QueryHistoryEntryId);
        Assert.Equal(1, deletedHistory);
        Assert.Single(auditAfterDelete);
    }

    private static AiRequest CreateRequest() =>
        new(
            AiPrivacyTier.MetadataOnly,
            "openai",
            "gpt-test",
            "recommendation",
            "Recommend a classroom reading plan",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma Library",
                ["author"] = "Chwezi Core Systems",
            });

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderKey => "openai";

        public bool IsLocalOnly => false;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletion("integration answer", 10, 5));
    }

    private sealed class CapturingPreviewGate : IAiPreviewGate
    {
        public int CallCount { get; private set; }

        public Task<AiPreviewDecision> ShowAsync(AiPayloadPreview preview, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(AiPreviewDecision.Send);
        }
    }
}
