using OgmaLibrary.Application.Ai;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 27 durable provider-profile and secret-boundary tests.</summary>
public sealed class Phase27ProviderProfileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogma-ai-profiles-{Guid.NewGuid():N}");

    [Fact]
    public async Task Profiles_RoundTripAtomically_AndNeverPersistRawCredential()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "profiles.json");
        using (var service = new AiProviderProfileService(path))
        {
            AiProviderProfile saved = await service.SaveAsync(new(
                "school-openai",
                "openai",
                "gpt-4.1-mini",
                new Uri("https://api.openai.com/v1/"),
                "credential:school-openai",
                Enabled: true,
                IsDefault: true,
                DateTimeOffset.MinValue));
            Assert.True(saved.UpdatedUtc > DateTimeOffset.MinValue);
        }

        string persisted = await File.ReadAllTextAsync(path);
        Assert.Contains("credential:school-openai", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live-secret", persisted, StringComparison.Ordinal);
        using var reloaded = new AiProviderProfileService(path);
        AiProviderProfile profile = Assert.Single(await reloaded.ListAsync());
        Assert.Equal("openai", profile.ProviderKey);
        Assert.True(await reloaded.DeleteAsync(profile.ProfileId));
        Assert.Empty(await reloaded.ListAsync());
    }

    [Fact]
    public async Task Profiles_RejectUntrustedEndpointsAndRawSecrets()
    {
        Directory.CreateDirectory(_directory);
        using var service = new AiProviderProfileService(Path.Combine(_directory, "profiles.json"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(
            "evil",
            "openai",
            "model",
            new Uri("https://evil.example/v1/"),
            "credential:ref",
            true,
            false,
            DateTimeOffset.UtcNow)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(
            "secret",
            "openai",
            "model",
            new Uri("https://api.openai.com/v1/"),
            "sk-live-secret",
            true,
            false,
            DateTimeOffset.UtcNow)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
