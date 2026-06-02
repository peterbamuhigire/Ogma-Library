using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
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
            await using (CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>())
            {
                await context.Database.MigrateAsync();
            }

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

    [Fact]
    public async Task SchoolAiKeyProvider_ReadsStatusFromCredentialStoreWithoutReturningPlaintext()
    {
        string dataDirectory = CreateTempDirectory();
        var credentialStore = new RecordingCredentialStore();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IClassroomCredentialStore>(credentialStore)
                .AddSchoolAdminServices(dataDirectory)
                .BuildServiceProvider();
            var keys = provider.GetRequiredService<ISchoolAiKeyProvider>();
            char[] key = "sk-ogma-school-test-key".ToCharArray();

            await keys.SaveKeyAsync(" OpenAI ", key);
            SchoolAiKeyStatus status = await keys.GetStatusAsync("openai");

            Assert.All(key, character => Assert.Equal('\0', character));
            Assert.Equal("openai", status.ProviderId);
            Assert.True(status.IsConfigured);
            Assert.NotNull(status.UpdatedUtc);
            Assert.Equal(SchoolAiKeyProvider.CreateCredentialKey("openai"), credentialStore.LastSavedKey);
            Assert.Contains("sk-ogma-school-test-key", credentialStore.LastSavedValue, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-ogma-school-test-key", status.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories),
                path => FileContainsAscii(path, "sk-ogma-school-test-key"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiKeyProvider_DeleteKeyClearsCredentialStatus()
    {
        string dataDirectory = CreateTempDirectory();
        var credentialStore = new RecordingCredentialStore();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IClassroomCredentialStore>(credentialStore)
                .AddSchoolAdminServices(dataDirectory)
                .BuildServiceProvider();
            var keys = provider.GetRequiredService<ISchoolAiKeyProvider>();

            await keys.SaveKeyAsync("openai", "sk-ogma-school-test-key".ToCharArray());
            await keys.DeleteKeyAsync("OPENAI");
            SchoolAiKeyStatus status = await keys.GetStatusAsync("openai");

            Assert.Equal(SchoolAiKeyProvider.CreateCredentialKey("openai"), credentialStore.LastDeletedKey);
            Assert.False(status.IsConfigured);
            Assert.Null(status.UpdatedUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SchoolAiKeyProvider_InvalidProviderId_ClearsInputBuffer()
    {
        string dataDirectory = CreateTempDirectory();
        var credentialStore = new RecordingCredentialStore();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IClassroomCredentialStore>(credentialStore)
                .AddSchoolAdminServices(dataDirectory)
                .BuildServiceProvider();
            var keys = provider.GetRequiredService<ISchoolAiKeyProvider>();
            char[] key = "sk-ogma-school-test-key".ToCharArray();

            await Assert.ThrowsAsync<ArgumentException>(() => keys.SaveKeyAsync("open ai", key));

            Assert.All(key, character => Assert.Equal('\0', character));
            Assert.Null(credentialStore.LastSavedKey);
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
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static bool FileContainsAscii(string path, string value)
    {
        byte[] fileBytes = File.ReadAllBytes(path);
        byte[] searchBytes = System.Text.Encoding.ASCII.GetBytes(value);
        return fileBytes.AsSpan().IndexOf(searchBytes) >= 0;
    }

    private sealed class RecordingCredentialStore : IClassroomCredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string? LastSavedKey { get; private set; }

        public string? LastSavedValue { get; private set; }

        public string? LastDeletedKey { get; private set; }

        public Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            cancellationToken.ThrowIfCancellationRequested();
            LastSavedKey = key;
            LastSavedValue = value;
            _secrets[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();
            _secrets.TryGetValue(key, out string? value);
            return Task.FromResult(value);
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();
            LastDeletedKey = key;
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}
