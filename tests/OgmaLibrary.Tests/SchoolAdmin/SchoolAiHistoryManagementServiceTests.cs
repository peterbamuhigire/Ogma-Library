using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 school AI history-management tests.</summary>
public sealed class SchoolAiHistoryManagementServiceTests
{
    [Fact]
    public async Task SchoolAiHistoryManagement_PurgeClearsQueryHistoryAndLedger_ButKeepsAudit()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            await using (CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>())
            {
                context.AiQueryHistory.Add(new AiQueryHistoryRow
                {
                    HistoryId = "history-1",
                    QueryType = "school-smart-search",
                    QueryText = "student question",
                    ResponseSummary = "answer",
                    CreatedUtc = DateTimeOffset.UtcNow,
                });
                context.AiUsageLedger.Add(new AiUsageLedgerRow
                {
                    Id = "profile-1:2026-06-02",
                    ProfileId = "profile-1",
                    Date = "2026-06-02",
                    QueryCount = 2,
                    TokensUsed = 120,
                    EstimatedCostUsd = 0.03m,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                });
                context.AuditEvents.Add(new AuditEventRow
                {
                    EventType = "SchoolAiSearch",
                    EntityType = "SchoolAi",
                    EntityId = "profile-1",
                    Timestamp = DateTimeOffset.UtcNow,
                    AfterJson = "{\"queryHash\":\"abc\"}",
                });
                await context.SaveChangesAsync();
            }

            var history = provider.GetRequiredService<ISchoolAiHistoryManagementService>();
            SchoolAiHistoryPurgeResult result = await history.PurgeInstitutionHistoryAsync();

            await using CatalogueDbContext verify = provider.GetRequiredService<CatalogueDbContext>();
            Assert.Equal(1, result.QueryHistoryRowsDeleted);
            Assert.Equal(1, result.UsageLedgerRowsDeleted);
            Assert.Empty(await verify.AiQueryHistory.ToListAsync());
            Assert.Empty(await verify.AiUsageLedger.ToListAsync());
            Assert.Single(await verify.AuditEvents.ToListAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiHistoryManagement_DisabledRegistrationThrows()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .BuildServiceProvider();
            var disabled = new UnavailableSchoolAdminService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => disabled.PurgeInstitutionHistoryAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-history-{Guid.NewGuid():N}");
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
