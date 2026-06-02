using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 data-backed school AI policy and quota tests.</summary>
public sealed class SchoolAiPolicyServiceTests
{
    [Fact]
    public async Task SchoolAiPolicyService_GetAndSavePolicy_UpdatesProfileEntitlements()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await enrollment.EnrollAsync(new EnrollProfileRequest("Amina Reader", "student", BirthYear: 2014));

            SchoolAiPolicy initial = await policy.GetPolicyAsync();
            await policy.SavePolicyAsync(new SchoolAiPolicy(
                AiPrivacyTier.MetadataOnly,
                ContentAwareEnabled: false,
                PerStudentDailyTokenBudget: 250,
                ClassDailyTokenBudget: 1_000,
                PerStudentQueriesPerMinute: 7,
                AnswerModeEnabled: false));
            SchoolAiPolicy updated = await policy.GetPolicyAsync();

            Assert.Equal(10_000, initial.PerStudentDailyTokenBudget);
            Assert.Equal(500_000, initial.ClassDailyTokenBudget);
            Assert.Equal(5, initial.PerStudentQueriesPerMinute);
            Assert.Equal(AiPrivacyTier.MetadataOnly, updated.DefaultTier);
            Assert.False(updated.ContentAwareEnabled);
            Assert.Equal(250, updated.PerStudentDailyTokenBudget);
            Assert.Equal(1_000, updated.ClassDailyTokenBudget);
            Assert.Equal(7, updated.PerStudentQueriesPerMinute);
            Assert.False(updated.AnswerModeEnabled);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiPolicyService_CheckAndReserveQuota_WritesUsageLedger()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = await EnrollAsync(provider, "Amina Reader");
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await policy.SavePolicyAsync(DefaultPolicy(perStudentTokens: 100, classTokens: 500));

            SchoolAiQuotaDecision decision = await policy.CheckAndReserveQuotaAsync(profileId, 25);

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            var ledger = await context.AiUsageLedger.SingleAsync();
            Assert.True(decision.IsAllowed);
            Assert.Equal(75, decision.RemainingStudentTokens);
            Assert.Equal(475, decision.RemainingClassTokens);
            Assert.Null(decision.Reason);
            Assert.Equal(profileId.ToString("D"), ledger.ProfileId);
            Assert.Equal(25, ledger.TokensUsed);
            Assert.Equal(1, ledger.QueryCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiPolicyService_CheckAndReserveQuota_BlocksStudentExhaustion()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = await EnrollAsync(provider, "Amina Reader");
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await policy.SavePolicyAsync(DefaultPolicy(perStudentTokens: 5, classTokens: 500));

            SchoolAiQuotaDecision decision = await policy.CheckAndReserveQuotaAsync(profileId, 10);

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            Assert.False(decision.IsAllowed);
            Assert.Equal(5, decision.RemainingStudentTokens);
            Assert.Equal(500, decision.RemainingClassTokens);
            Assert.Contains("student", decision.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await context.AiUsageLedger.ToListAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiPolicyService_CheckAndReserveQuota_BlocksClassExhaustion()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid firstProfileId = await EnrollAsync(provider, "Amina Reader");
            Guid secondProfileId = await EnrollAsync(provider, "Okello Reader");
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await policy.SavePolicyAsync(DefaultPolicy(perStudentTokens: 100, classTokens: 15));

            SchoolAiQuotaDecision first = await policy.CheckAndReserveQuotaAsync(firstProfileId, 10);
            SchoolAiQuotaDecision second = await policy.CheckAndReserveQuotaAsync(secondProfileId, 10);

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            Assert.True(first.IsAllowed);
            Assert.False(second.IsAllowed);
            Assert.Equal(100, second.RemainingStudentTokens);
            Assert.Equal(5, second.RemainingClassTokens);
            Assert.Contains("class", second.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(10, await context.AiUsageLedger.SumAsync(row => row.TokensUsed));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiPolicyService_ConcurrentReservations_DoNotOverrunBudget()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = await EnrollAsync(provider, "Amina Reader");
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await policy.SavePolicyAsync(DefaultPolicy(perStudentTokens: 15, classTokens: 100));

            SchoolAiQuotaDecision[] decisions = await Task.WhenAll(Enumerable
                .Range(0, 20)
                .Select(_ => policy.CheckAndReserveQuotaAsync(profileId, 1)));

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            var ledger = await context.AiUsageLedger.SingleAsync();
            Assert.Equal(15, decisions.Count(decision => decision.IsAllowed));
            Assert.Equal(5, decisions.Count(decision => !decision.IsAllowed));
            Assert.Equal(15, ledger.TokensUsed);
            Assert.Equal(15, ledger.QueryCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiPolicyService_SavePolicy_RejectsUnsupportedTierElevation()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => policy.SavePolicyAsync(new SchoolAiPolicy(
                AiPrivacyTier.ContentAware,
                ContentAwareEnabled: true,
                PerStudentDailyTokenBudget: 100,
                ClassDailyTokenBudget: 500,
                PerStudentQueriesPerMinute: 5,
                AnswerModeEnabled: false)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static SchoolAiPolicy DefaultPolicy(int perStudentTokens, int classTokens) =>
        new(
            AiPrivacyTier.MetadataOnly,
            ContentAwareEnabled: false,
            perStudentTokens,
            classTokens,
            PerStudentQueriesPerMinute: 5,
            AnswerModeEnabled: false);

    private static async Task<Guid> EnrollAsync(ServiceProvider provider, string displayName)
    {
        var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
        EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
            displayName,
            "student",
            BirthYear: 2014));
        return token.ProfileId;
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddSchoolAdminServices(dataDirectory)
            .BuildServiceProvider();
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-policy-{Guid.NewGuid():N}");
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
}
