using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 school DPIA screening tests.</summary>
public sealed class SchoolDpiaScreeningServiceTests
{
    [Fact]
    public async Task DpiaScreening_MetadataOnlyMinorProfile_ApprovedAndAudited()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            Guid profileId = Guid.NewGuid();

            DpiaScreeningResult result = await provider.GetRequiredService<IDpiaScreeningService>()
                .CheckAsync(new DpiaScreeningRequest(
                    profileId,
                    AiPrivacyTier.MetadataOnly,
                    "metadata",
                    BirthYear: DateTimeOffset.UtcNow.Year - 12));

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            var audit = await context.AuditEvents.SingleAsync(eventRow => eventRow.EventType == "SchoolDpiaScreened");
            Assert.Equal(DpiaScreeningDecision.Approved, result.Decision);
            Assert.Equal(profileId.ToString("D"), audit.EntityId);
            Assert.Contains("MetadataOnly", audit.AfterJson, StringComparison.Ordinal);
            Assert.Contains("Approved", audit.AfterJson, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2014)]
    public async Task DpiaScreening_ContentAwareMinorOrUnknownAge_Disqualified(int? birthYear)
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);

            DpiaScreeningResult result = await provider.GetRequiredService<IDpiaScreeningService>()
                .CheckAsync(new DpiaScreeningRequest(
                    Guid.NewGuid(),
                    AiPrivacyTier.ContentAware,
                    "book-content",
                    birthYear));

            Assert.Equal(DpiaScreeningDecision.Disqualified, result.Decision);
            Assert.Contains("minors", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Theory]
    [InlineData(AiPrivacyTier.Offline)]
    [InlineData(AiPrivacyTier.LocalOllama)]
    public async Task DpiaScreening_NoOffDeviceEgressTiers_Approved(AiPrivacyTier tier)
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);

            DpiaScreeningResult result = await provider.GetRequiredService<IDpiaScreeningService>()
                .CheckAsync(new DpiaScreeningRequest(
                    Guid.NewGuid(),
                    tier,
                    "local",
                    BirthYear: null));

            Assert.Equal(DpiaScreeningDecision.Approved, result.Decision);
            Assert.Contains("no off-device", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task DpiaScreening_RejectsMissingPayloadScope()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => provider.GetRequiredService<IDpiaScreeningService>()
                .CheckAsync(new DpiaScreeningRequest(
                    Guid.NewGuid(),
                    AiPrivacyTier.MetadataOnly,
                    " ",
                    BirthYear: null)));
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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-dpia-{Guid.NewGuid():N}");
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
