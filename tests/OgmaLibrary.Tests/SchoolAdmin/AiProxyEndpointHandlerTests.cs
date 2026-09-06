using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 Host-side classroom AI proxy tests.</summary>
public sealed class AiProxyEndpointHandlerTests
{
    [Fact]
    public async Task SearchAsync_ConfirmedMetadataRequest_ReservesQuotaScreensDpiaAndGroundsCitations()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provider = new FakeProvider("Use LAN Endpoint Book [[book:01LANENDPOINT000000000001]] and not [[book:01FAKEBOOK000000000001]].");
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();

            AiProxyPayloadPreview preview = await handler.PreviewAsync(Request(profileId, confirmed: false));
            AiProxySearchResult result = await handler.SearchAsync(Request(profileId, confirmed: true));

            await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
            AiUsageLedgerRow ledger = await context.AiUsageLedger.SingleAsync();
            AuditEventRow dpiaAudit = await context.AuditEvents.SingleAsync(row => row.EventType == "SchoolDpiaScreened");

            Assert.True(preview.RequiresConfirmation);
            Assert.Equal(AiPrivacyTier.MetadataOnly, preview.Tier);
            Assert.Contains("01LANENDPOINT000000000001", preview.MetadataFields["candidates"], StringComparison.Ordinal);
            Assert.Equal(1, provider.Calls);
            Assert.True(result.WasProviderCalled);
            Assert.Equal("Use LAN Endpoint Book and not .", result.Answer);
            GroundedCitation citation = Assert.Single(result.Citations);
            Assert.Equal("01LANENDPOINT000000000001", citation.BookId);
            Assert.Equal("LAN Endpoint Book", citation.Title);
            Assert.Equal(profileId.ToString("D"), ledger.ProfileId);
            Assert.True(ledger.TokensUsed > 0);
            Assert.Equal(1, ledger.QueryCount);
            Assert.Equal(profileId.ToString("D"), dpiaAudit.EntityId);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchAsync_WithoutConfirmedPreview_BlocksBeforeProviderAndQuota()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provider = new FakeProvider("Use LAN Endpoint Book [[book:01LANENDPOINT000000000001]].");
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();

            SchoolAiProxyException error = await Assert.ThrowsAsync<SchoolAiProxyException>(() =>
                handler.SearchAsync(Request(profileId, confirmed: false)));

            await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
            Assert.Equal("payload_preview_required", error.Code);
            Assert.Equal(400, error.StatusCode);
            Assert.Equal(0, provider.Calls);
            Assert.Empty(await context.AiUsageLedger.ToListAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchAsync_RateLimit_BlocksRepeatedCallsBeforeSecondProviderCall()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provider = new FakeProvider("Use LAN Endpoint Book [[book:01LANENDPOINT000000000001]].");
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            await services.GetRequiredService<ISchoolAiPolicyService>()
                .SavePolicyAsync(new SchoolAiPolicy(
                    AiPrivacyTier.MetadataOnly,
                    ContentAwareEnabled: false,
                    PerStudentDailyTokenBudget: 1_000,
                    ClassDailyTokenBudget: 10_000,
                    PerStudentQueriesPerMinute: 1,
                    AnswerModeEnabled: false));
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();

            await handler.SearchAsync(Request(profileId, confirmed: true));
            SchoolAiProxyException error = await Assert.ThrowsAsync<SchoolAiProxyException>(() =>
                handler.SearchAsync(Request(profileId, confirmed: true)));

            Assert.Equal("school_ai_rate_limited", error.Code);
            Assert.Equal(429, error.StatusCode);
            Assert.Equal(1, provider.Calls);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchAsync_WithoutProvider_FailsClosedBeforeQuotaReservation()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider: null);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();

            SchoolAiProxyException error = await Assert.ThrowsAsync<SchoolAiProxyException>(() =>
                handler.SearchAsync(Request(profileId, confirmed: true)));

            await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
            Assert.Equal("school_ai_provider_unavailable", error.Code);
            Assert.Equal(503, error.StatusCode);
            Assert.Empty(await context.AiUsageLedger.ToListAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchAsync_ExhaustedQuota_BlocksBeforeProviderCall()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provider = new FakeProvider("No call expected.");
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            await services.GetRequiredService<ISchoolAiPolicyService>()
                .SavePolicyAsync(new SchoolAiPolicy(
                    AiPrivacyTier.MetadataOnly,
                    ContentAwareEnabled: false,
                    PerStudentDailyTokenBudget: 0,
                    ClassDailyTokenBudget: 10_000,
                    PerStudentQueriesPerMinute: 5,
                    AnswerModeEnabled: false));
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();

            SchoolAiProxyException error = await Assert.ThrowsAsync<SchoolAiProxyException>(() =>
                handler.SearchAsync(Request(profileId, confirmed: true)));

            Assert.Equal("school_ai_quota_exhausted", error.Code);
            Assert.Equal(429, error.StatusCode);
            Assert.Equal(0, provider.Calls);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchAsync_OverlongQuery_IsRejectedBeforeProviderOrQuota()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provider = new FakeProvider("No call expected.");
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory, provider);
            await SeedBookAsync(services);
            Guid profileId = await EnrollAsync(services);
            var handler = services.GetRequiredService<IAiProxyEndpointHandler>();
            string longQuery = new('x', 257);

            SchoolAiProxyException error = await Assert.ThrowsAsync<SchoolAiProxyException>(() =>
                handler.SearchAsync(new AiProxySearchRequest(
                    profileId,
                    longQuery,
                    "default",
                    AiPrivacyTier.MetadataOnly,
                    ConfirmedPayloadPreview: true)));

            Assert.Equal("invalid_query", error.Code);
            Assert.Equal(0, provider.Calls);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory, IAiProvider? provider)
    {
        ServiceCollection services = new();
        services
            .AddCatalogueContext(dataDirectory, dataDirectory);
        if (provider is not null)
        {
            services.AddSingleton(provider);
        }

        services.AddSchoolAdminServices(dataDirectory);
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        await using CatalogueDbContext context = serviceProvider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return serviceProvider;
    }

    private static async Task<Guid> EnrollAsync(ServiceProvider services)
    {
        EnrollmentToken token = await services.GetRequiredService<IProfileEnrollmentService>()
            .EnrollAsync(new EnrollProfileRequest("Amina Reader", "student", BirthYear: 2014));
        return token.ProfileId;
    }

    private static async Task SeedBookAsync(ServiceProvider services)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        context.Books.Add(new BookRow
        {
            BookId = "01LANENDPOINT000000000001",
            Title = "LAN Endpoint Book",
            RelativePath = "lan-endpoint-book.pdf",
            Sha256Hash = new string('d', 64),
            SizeBytes = 128,
            MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
            Status = 0,
            IndexStatus = 0,
            EmbeddingStatus = 0,
            IsOcrDerived = false,
            IsPasswordProtected = false,
            Year = 2026,
            BookFiles =
            [
                new BookFileRow
                {
                    RelativePath = "lan-endpoint-book.pdf",
                    FileStatus = 0,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                },
            ],
        });
        await context.SaveChangesAsync();
    }

    private static AiProxySearchRequest Request(Guid profileId, bool confirmed) =>
        new(
            profileId,
            "LAN Endpoint",
            "default",
            AiPrivacyTier.MetadataOnly,
            confirmed);

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-ai-proxy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private sealed class FakeProvider(string completionText) : IAiProvider
    {
        public string ProviderKey => "fake";

        public bool IsLocalOnly => false;

        public int Calls { get; private set; }

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AiCompletion(completionText, PromptTokens: 25, CompletionTokens: 10));
        }
    }
}
