using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 data-backed school AI usage dashboard tests.</summary>
public sealed class SchoolUsageDashboardServiceTests
{
    [Fact]
    public async Task UsageDashboard_ReturnsPerProfileCountsCostsAndQuotaPercent()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid firstProfileId = await EnrollAsync(provider, "Amina Reader");
            Guid secondProfileId = await EnrollAsync(provider, "Okello Reader");
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            await policy.SavePolicyAsync(new SchoolAiPolicy(
                OgmaLibrary.Domain.Ai.AiPrivacyTier.MetadataOnly,
                ContentAwareEnabled: false,
                PerStudentDailyTokenBudget: 100,
                ClassDailyTokenBudget: 500,
                PerStudentQueriesPerMinute: 5,
                AnswerModeEnabled: false));
            await AddLedgerAsync(provider, firstProfileId, Today().AddDays(-1), tokens: 25, queries: 4, cost: 0.12m);
            await AddLedgerAsync(provider, firstProfileId, Today(), tokens: 35, queries: 6, cost: 0.18m);
            await AddLedgerAsync(provider, secondProfileId, Today(), tokens: 50, queries: 5, cost: 0.25m);

            IReadOnlyList<UsageDashboardEntry> entries = await provider
                .GetRequiredService<IUsageDashboardService>()
                .GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

            UsageDashboardEntry first = Assert.Single(entries, entry => entry.ProfileId == firstProfileId);
            UsageDashboardEntry second = Assert.Single(entries, entry => entry.ProfileId == secondProfileId);
            Assert.Equal("Amina Reader", first.DisplayName);
            Assert.Equal(10, first.QueryCount);
            Assert.Equal(60, first.TokensUsed);
            Assert.Equal(0.30m, first.EstimatedCostUsd);
            Assert.Equal(60d, first.QuotaPercent);
            Assert.NotNull(first.LastQueryUtc);
            Assert.Equal("Okello Reader", second.DisplayName);
            Assert.Equal(5, second.QueryCount);
            Assert.Equal(50, second.TokensUsed);
            Assert.Equal(0.25m, second.EstimatedCostUsd);
            Assert.Equal(50d, second.QuotaPercent);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task UsageDashboard_FiltersByUtcDateRange()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = await EnrollAsync(provider, "Amina Reader");
            await AddLedgerAsync(provider, profileId, DateTimeOffset.UtcNow.AddDays(-3), tokens: 80, queries: 8, cost: 0.80m);
            await AddLedgerAsync(provider, profileId, Today(), tokens: 20, queries: 2, cost: 0.20m);

            UsageDashboardEntry entry = Assert.Single(await provider
                .GetRequiredService<IUsageDashboardService>()
                .GetSummaryAsync(DateTimeOffset.UtcNow.AddHours(-12), DateTimeOffset.UtcNow));

            Assert.Equal(2, entry.QueryCount);
            Assert.Equal(20, entry.TokensUsed);
            Assert.Equal(0.20m, entry.EstimatedCostUsd);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task UsageDashboard_IncludesEnrolledProfilesWithZeroUsage()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = await EnrollAsync(provider, "Amina Reader");

            UsageDashboardEntry entry = Assert.Single(await provider
                .GetRequiredService<IUsageDashboardService>()
                .GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));

            Assert.Equal(profileId, entry.ProfileId);
            Assert.Equal("Amina Reader", entry.DisplayName);
            Assert.Equal(0, entry.QueryCount);
            Assert.Equal(0, entry.TokensUsed);
            Assert.Equal(0m, entry.EstimatedCostUsd);
            Assert.Equal(0d, entry.QuotaPercent);
            Assert.Null(entry.LastQueryUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task UsageDashboard_RejectsInvalidRange()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var dashboard = provider.GetRequiredService<IUsageDashboardService>();

            await Assert.ThrowsAsync<ArgumentException>(() => dashboard.GetSummaryAsync(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(-1)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<Guid> EnrollAsync(ServiceProvider provider, string displayName)
    {
        var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
        EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
            displayName,
            "student",
            BirthYear: 2014));
        return token.ProfileId;
    }

    private static async Task AddLedgerAsync(
        ServiceProvider provider,
        Guid profileId,
        DateTimeOffset date,
        int tokens,
        int queries,
        decimal cost)
    {
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        string formattedDate = date.UtcDateTime.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        context.AiUsageLedger.Add(new AiUsageLedgerRow
        {
            Id = $"{profileId:D}:{formattedDate}",
            ProfileId = profileId.ToString("D"),
            Date = formattedDate,
            TokensUsed = tokens,
            QueryCount = queries,
            EstimatedCostUsd = cost,
            UpdatedUtc = date,
        });
        await context.SaveChangesAsync();
    }

    private static DateTimeOffset Today() => DateTimeOffset.UtcNow;

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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-dashboard-{Guid.NewGuid():N}");
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
