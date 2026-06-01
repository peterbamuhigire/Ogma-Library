using System.Text.Json;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 Privacy Center tests.</summary>
public sealed class PrivacyCenterViewModelTests
{
    [Fact]
    public async Task PrivacyCenter_DeleteHistory_LeavesAuditIntact()
    {
        var audit = new FakeAuditRepository();
        var history = new FakeHistoryRepository { DeleteCount = 2 };
        using var viewModel = CreateViewModel(audit: audit, history: history);

        await viewModel.LoadAsync();
        int deleted = await viewModel.DeleteHistoryAsync();
        await using var stream = new MemoryStream();
        await viewModel.ExportAuditAsync(stream);
        stream.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(2, deleted);
        Assert.Single(viewModel.RecentCalls);
        Assert.Single(audit.Events);
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task PrivacyCenter_SetTier_UpdatesPrivacyService()
    {
        var consents = new FakeConsentRepository();
        var privacy = new AiPrivacyService(consents, new AiPayloadBuilder());
        using var viewModel = CreateViewModel(privacy: privacy);

        await viewModel.SetTierAsync(AiPrivacyTier.ContentAware);

        Assert.Equal(AiPrivacyTier.ContentAware, viewModel.ActiveTier);
        Assert.Equal(AiPrivacyTier.ContentAware, privacy.GetActiveTier());
    }

    [Fact]
    public async Task PrivacyService_RequiresActiveConsent()
    {
        var consents = new FakeConsentRepository();
        var privacy = new AiPrivacyService(consents, new AiPayloadBuilder());
        var consent = new AiConsentRecord(
            "consent-1",
            AiPrivacyTier.MetadataOnly,
            "openai",
            "library:default",
            DateTimeOffset.UtcNow);

        await privacy.RecordConsentAsync(consent, CancellationToken.None);
        bool hasConsent = await privacy.HasConsentAsync(
            AiPrivacyTier.MetadataOnly,
            "openai",
            "library:default",
            CancellationToken.None);

        Assert.True(hasConsent);
    }

    private static PrivacyCenterViewModel CreateViewModel(
        IAiPrivacyService? privacy = null,
        FakeAuditRepository? audit = null,
        FakeHistoryRepository? history = null) =>
        new(
            privacy ?? new AiPrivacyService(new FakeConsentRepository(), new AiPayloadBuilder()),
            audit ?? new FakeAuditRepository(),
            history ?? new FakeHistoryRepository(),
            new FakeEmbeddingErasureService(),
            new InMemoryLocalizationService());

    private sealed class FakeConsentRepository : IAiConsentRepository
    {
        private readonly List<AiConsentRecord> _consents = [];

        public Task UpsertAsync(AiConsentRecord consent, CancellationToken cancellationToken)
        {
            _consents.RemoveAll(existing => existing.Id == consent.Id);
            _consents.Add(consent);
            return Task.CompletedTask;
        }

        public Task<AiConsentRecord?> GetActiveConsentAsync(
            AiPrivacyTier tier,
            string provider,
            string scope,
            CancellationToken cancellationToken) =>
            Task.FromResult(_consents.FirstOrDefault(consent =>
                consent.Tier == tier &&
                consent.Provider == provider &&
                consent.Scope == scope &&
                consent.IsActive));

        public Task<int> RevokeAllAsync(
            AiPrivacyTier tier,
            DateTimeOffset revokedAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class FakeAuditRepository : IAiAuditRepository
    {
        public List<AiAuditEvent> Events { get; } =
        [
            new AiAuditEvent(
                "audit-1",
                DateTimeOffset.UtcNow,
                AiPrivacyTier.MetadataOnly,
                "openai",
                "gpt-test",
                new string('a', 64),
                new string('b', 64)),
        ];

        public Task AppendAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AiAuditEvent>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiAuditEvent>>(Events.Take(count).ToList());

        public async Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken)
        {
            await JsonSerializer.SerializeAsync(output, Events, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class FakeHistoryRepository : IAiQueryHistoryRepository
    {
        public int DeleteCount { get; init; }

        public Task AddAsync(AiQueryHistoryEntry entry, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AiQueryHistoryEntry>> ListAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiQueryHistoryEntry>>([]);

        public Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<int> HardDeleteAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DeleteCount);
    }

    private sealed class FakeEmbeddingErasureService : IEmbeddingErasureService
    {
        public Task<EmbeddingErasureResult> EraseAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingErasureResult(3, 2, DateTimeOffset.UtcNow));
    }
}
