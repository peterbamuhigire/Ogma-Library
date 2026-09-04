using OgmaLibrary.Application.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 30 consent, minimization, retention, and bounds checks.</summary>
public sealed class Phase30AdvisorFeedbackTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"ogma-feedback-{Guid.NewGuid():N}");

    [Fact]
    public async Task Submit_RequiresExplicitConsent_AndPersistsOnlyBoundedFields()
    {
        using var service = new AdvisorFeedbackService(Path.Combine(_directory, "feedback.json"));
        AdvisorFeedbackEntry entry = Entry();

        await Assert.ThrowsAsync<AdvisorFeedbackConsentRequiredException>(() =>
            service.SubmitAsync(entry, consentGranted: false));
        Assert.False(File.Exists(Path.Combine(_directory, "feedback.json")));

        AdvisorFeedbackEntry saved = await service.SubmitAsync(entry, consentGranted: true);
        Assert.Equal(entry.FeedbackId, saved.FeedbackId);
        Assert.NotEqual(default, saved.SubmittedUtc);

        string json = await File.ReadAllTextAsync(Path.Combine(_directory, "feedback.json"));
        Assert.Contains(entry.RequestHash, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generated answer", json, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await service.ListAsync());
    }

    [Fact]
    public async Task InvalidHashAndRating_AreRejected()
    {
        using var service = new AdvisorFeedbackService(Path.Combine(_directory, "feedback.json"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitAsync(Entry() with { RequestHash = "not-a-hash" }, true));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SubmitAsync(Entry() with { Rating = 6 }, true));
    }

    [Fact]
    public async Task Feedback_ReloadsAcrossInstances()
    {
        string path = Path.Combine(_directory, "feedback.json");
        using (var service = new AdvisorFeedbackService(path))
        {
            await service.SubmitAsync(Entry(), true);
        }

        using var reloaded = new AdvisorFeedbackService(path);
        AdvisorFeedbackEntry saved = Assert.Single(await reloaded.ListAsync());
        Assert.Equal("too-vague", saved.ReasonCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static AdvisorFeedbackEntry Entry() => new(
        "feedback-1",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        3,
        "too-vague",
        DateTimeOffset.UnixEpoch);
}
