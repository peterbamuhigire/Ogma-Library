using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 School Administration scaffold tests.</summary>
public sealed class SchoolAdminScaffoldTests
{
    [Fact]
    public async Task SchoolAdminServices_RegisterDisabledScaffold()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddSchoolAdminServices(dataDirectory)
                .BuildServiceProvider();

            var publishing = provider.GetRequiredService<ILibraryPublishingService>();
            var policy = provider.GetRequiredService<ISchoolAiPolicyService>();
            var dpia = provider.GetRequiredService<IDpiaScreeningService>();
            var dashboard = provider.GetRequiredService<IUsageDashboardService>();

            SchoolAiPolicy currentPolicy = await policy.GetPolicyAsync();
            DpiaScreeningResult dpiaResult = await dpia.CheckAsync(new DpiaScreeningRequest(
                Guid.NewGuid(),
                OgmaLibrary.Domain.Ai.AiPrivacyTier.MetadataOnly,
                "metadata",
                BirthYear: null));
            IReadOnlyList<UsageDashboardEntry> usage = await dashboard.GetSummaryAsync(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow);

            Assert.Empty(await publishing.ListAsync());
            Assert.Equal(OgmaLibrary.Domain.Ai.AiPrivacyTier.MetadataOnly, currentPolicy.DefaultTier);
            Assert.False(currentPolicy.ContentAwareEnabled);
            Assert.Equal(DpiaScreeningDecision.Disqualified, dpiaResult.Decision);
            Assert.Empty(usage);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("Admin", true)]
    [InlineData(" ADMIN ", true)]
    [InlineData("teacher", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SchoolAdminAuthorization_RecognizesOnlyAdminRole(string? role, bool expected)
    {
        Assert.Equal(expected, SchoolAdminAuthorization.IsAdminRole(role));
    }

    [Fact]
    public async Task SchoolAiKeyProvider_SaveKey_ClearsInputBufferEvenWhenDisabled()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddSchoolAdminServices(dataDirectory)
                .BuildServiceProvider();
            var keys = provider.GetRequiredService<ISchoolAiKeyProvider>();
            char[] key = "sk-test-secret".ToCharArray();

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => keys.SaveKeyAsync("openai", key));
            SchoolAiKeyStatus status = await keys.GetStatusAsync("openai");

            Assert.Contains("not enabled", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.All(key, character => Assert.Equal('\0', character));
            Assert.False(status.IsConfigured);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
