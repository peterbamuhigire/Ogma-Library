using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Settings;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Render checks for the Phase 18 school administration panel.</summary>
public sealed class SchoolAdminPanelRenderTests
{
    [AvaloniaFact]
    public async Task SharingSettingsView_WithSchoolAdminServices_RendersAdminConsole()
    {
        var viewModel = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            profileEnrollmentService: new FakeProfileEnrollmentService(),
            schoolAiKeyProvider: new FakeSchoolAiKeyProvider(),
            schoolAiPolicyService: new FakeSchoolAiPolicyService(),
            usageDashboardService: new FakeUsageDashboardService(),
            schoolAiHistoryManagementService: new FakeSchoolAiHistoryManagementService(),
            auditRepository: new FakeAuditRepository());
        await viewModel.RefreshSchoolAdminAsync();

        var view = new SharingSettingsView { DataContext = viewModel };
        var window = new Window
        {
            Width = 1280,
            Height = 900,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        List<string?> visibleText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text)
            .ToList();

        Assert.Contains("School administration", visibleText);
        Assert.Contains("Managed profiles", visibleText);
        Assert.Contains("School AI", visibleText);
        Assert.Contains("Usage dashboard", visibleText);
        Assert.Contains("Recent audit", visibleText);
        Assert.Contains("Export CSV", visibleText);
        Assert.Contains("Purge AI history", visibleText);
        Assert.Contains("Amina Reader", visibleText);
    }

    private sealed class FakeHostModeSettingsRepository : IHostModeSettingsRepository
    {
        public Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HostModeSettings(false, 7473, HostContentDeliveryMode.PageRender, "Ogma Test Host"));

        public Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeLibraryHostService : ILibraryHostService
    {
        public Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default) =>
            GetStatusAsync(cancellationToken);

        public Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default) =>
            GetStatusAsync(cancellationToken);

        public Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryHostStatus(
                LibraryHostState.Stopped,
                Port: 7473,
                ConnectedClientCount: 0,
                CertificateFingerprint: null,
                ErrorMessage: null));
    }

    private sealed class FakeProfileEnrollmentService : IProfileEnrollmentService
    {
        public Task<EnrollmentToken> EnrollAsync(
            EnrollProfileRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EnrollmentToken(Guid.NewGuid(), "token", DateTimeOffset.UtcNow.AddHours(24)));

        public Task<IReadOnlyList<EnrolledProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EnrolledProfile>>(
            [
                new(
                    Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                    "Amina Reader",
                    "student",
                    EnrollmentStatus.Active,
                    2014,
                    DateTimeOffset.UtcNow,
                    RevokedUtc: null),
            ]);

        public Task RevokeAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EnrolledProfile?> RedeemTokenAsync(
            Guid profileId,
            string token,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EnrolledProfile?>(null);
    }

    private sealed class FakeSchoolAiKeyProvider : ISchoolAiKeyProvider
    {
        public Task SaveKeyAsync(string providerId, char[] key, CancellationToken cancellationToken = default)
        {
            Array.Clear(key);
            return Task.CompletedTask;
        }

        public Task<SchoolAiKeyStatus> GetStatusAsync(string providerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchoolAiKeyStatus(providerId, IsConfigured: true, DateTimeOffset.UtcNow));

        public Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSchoolAiPolicyService : ISchoolAiPolicyService
    {
        public Task<SchoolAiPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchoolAiPolicy(
                AiPrivacyTier.MetadataOnly,
                ContentAwareEnabled: false,
                PerStudentDailyTokenBudget: 250,
                ClassDailyTokenBudget: 1_000,
                PerStudentQueriesPerMinute: 7,
                AnswerModeEnabled: false));

        public Task SavePolicyAsync(SchoolAiPolicy policy, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SchoolAiQuotaDecision> CheckAndReserveQuotaAsync(
            Guid profileId,
            int estimatedTokens,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchoolAiQuotaDecision(true, 100, 500, DateTimeOffset.UtcNow.AddDays(1), Reason: null));
    }

    private sealed class FakeUsageDashboardService : IUsageDashboardService
    {
        public Task<IReadOnlyList<UsageDashboardEntry>> GetSummaryAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UsageDashboardEntry>>(
            [
                new(
                    Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                    "Amina Reader",
                    QueryCount: 3,
                    TokensUsed: 120,
                    EstimatedCostUsd: 0.01m,
                    QuotaPercent: 12,
                    LastQueryUtc: DateTimeOffset.UtcNow),
            ]);
    }

    private sealed class FakeSchoolAiHistoryManagementService : ISchoolAiHistoryManagementService
    {
        public Task<SchoolAiHistoryPurgeResult> PurgeInstitutionHistoryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchoolAiHistoryPurgeResult(0, 0, DateTimeOffset.UtcNow));
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AuditEvent>> ReadRecentAsync(int maxCount, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>(
            [
                new()
                {
                    Id = "audit-1",
                    EventType = "LanHostRequestServed",
                    EntityId = "/api/v1/ai/search",
                    ActorId = "client:aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Payload = "{\"action\":\"SearchSchoolAi\"}",
                },
            ]);
    }
}
