using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 27 durable AI usage-budget enforcement tests.</summary>
public sealed class Phase27UsageBudgetTests
{
    [Fact]
    public async Task Reserve_RejectsWhenTokenBudgetWouldBeExceeded()
    {
        string path = CreateTemporaryPath();
        try
        {
            var service = new AiUsageBudgetService(
                new JsonAiUsageBudgetStore(path),
                new AiUsageBudgetLimits(DailyTokenLimit: 1_100, DailyCostUsdLimit: 10m));

            AiRequest request = CreateRequest(new string('x', 400));

            await Assert.ThrowsAsync<AiUsageBudgetExceededException>(() =>
                service.ReserveAsync(request, CancellationToken.None));
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task Finalize_PersistsUsageAndRestoresAcrossServiceInstances()
    {
        string path = CreateTemporaryPath();
        try
        {
            var limits = new AiUsageBudgetLimits(DailyTokenLimit: 10_000, DailyCostUsdLimit: 10m);
            var first = new AiUsageBudgetService(new JsonAiUsageBudgetStore(path), limits);
            AiUsageBudgetReservation reservation = await first.ReserveAsync(
                CreateRequest("recommend"),
                CancellationToken.None);

            await first.FinalizeAsync(
                reservation,
                new AiCompletion("grounded answer", PromptTokens: 10, CompletionTokens: 5),
                0.25m,
                CancellationToken.None);

            var restored = new AiUsageBudgetService(new JsonAiUsageBudgetStore(path), limits);
            AiUsageBudgetSnapshot snapshot = await restored.GetSnapshotAsync(CancellationToken.None);

            Assert.Equal(15, snapshot.UsedTokens);
            Assert.Equal(0.25m, snapshot.UsedCostUsd);
            Assert.Equal(9_985, snapshot.RemainingTokens);
            Assert.Equal(9.75m, snapshot.RemainingCostUsd);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public async Task Reserve_RejectsWhenPersistedCostBudgetIsExhausted()
    {
        string path = CreateTemporaryPath();
        try
        {
            var store = new JsonAiUsageBudgetStore(path);
            store.Save(new AiUsageBudgetState(DateOnly.FromDateTime(DateTime.UtcNow), 0, 0, 1m));
            var service = new AiUsageBudgetService(
                store,
                new AiUsageBudgetLimits(DailyTokenLimit: 10_000, DailyCostUsdLimit: 1m));

            await Assert.ThrowsAsync<AiUsageBudgetExceededException>(() =>
                service.ReserveAsync(CreateRequest("recommend"), CancellationToken.None));
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    private static AiRequest CreateRequest(string query) => new(
        AiPrivacyTier.MetadataOnly,
        "openai",
        "gpt-test",
        "recommendation",
        query);

    private static string CreateTemporaryPath() => Path.Combine(
        Path.GetTempPath(),
        $"ogma-ai-budget-{Guid.NewGuid():N}.json");

    private static void DeleteTemporaryFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
