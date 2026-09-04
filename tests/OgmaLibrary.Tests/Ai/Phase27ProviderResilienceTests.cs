using System.Net;
using System.Net.Http;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 27 provider timeout, retry and circuit-control tests.</summary>
public sealed class Phase27ProviderResilienceTests
{
    [Fact]
    public void ProviderHealthStore_RestoresRedactedOperationalState()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-ai-health-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAiProviderHealthStore(path);
            store.Save(
            [
                new AiProviderHealthSnapshot(
                    "openai",
                    ConsecutiveFailures: 2,
                    TotalFailures: 7,
                    TotalRetries: 3,
                    CircuitOpenUntilUtc: DateTimeOffset.UtcNow.AddMinutes(1)),
            ]);

            var registry = new AiProviderHealthRegistry(store);
            AiProviderHealthSnapshot snapshot = registry.GetSnapshot("openai");

            Assert.Equal(2, snapshot.ConsecutiveFailures);
            Assert.Equal(7, snapshot.TotalFailures);
            Assert.Equal(3, snapshot.TotalRetries);
            Assert.True(snapshot.CircuitOpenUntilUtc > DateTimeOffset.UtcNow);
            string persisted = File.ReadAllText(path);
            Assert.DoesNotContain("prompt", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("response", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("endpoint", persisted, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ResilientProvider_RetriesTransientFailureAndRecordsTelemetry()
    {
        var health = new AiProviderHealthRegistry();
        var inner = new StubProvider(
            "test",
            (_, attempt) => attempt == 1
                ? throw new HttpRequestException("temporary")
                : new AiCompletion("ok"));
        var provider = new ResilientAiProvider(
            inner,
            health,
            new AiProviderResilienceOptions { AttemptTimeout = TimeSpan.FromSeconds(1) });

        AiCompletion completion = await provider.CompleteAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("ok", completion.Text);
        AiProviderHealthSnapshot snapshot = health.GetSnapshot("test");
        Assert.Equal(1, snapshot.TotalRetries);
        Assert.Equal(0, snapshot.TotalFailures);
    }

    [Fact]
    public async Task ResilientProvider_OpensCircuitAfterBoundedFailures()
    {
        var health = new AiProviderHealthRegistry();
        var inner = new StubProvider(
            "test",
            (Func<AiRequest, int, AiCompletion>)((_, _) => throw new HttpRequestException("down")));
        var provider = new ResilientAiProvider(
            inner,
            health,
            new AiProviderResilienceOptions
            {
                MaxAttempts = 1,
                FailureThreshold = 2,
                CircuitOpenDuration = TimeSpan.FromMinutes(1),
                AttemptTimeout = TimeSpan.FromSeconds(1),
            });

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.CompleteAsync(CreateRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.CompleteAsync(CreateRequest(), CancellationToken.None));
        InvalidOperationException circuit = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CompleteAsync(CreateRequest(), CancellationToken.None));

        Assert.Contains("circuit is open", circuit.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, health.GetSnapshot("test").TotalFailures);
    }

    [Fact]
    public async Task ResilientProvider_HonorsCallerCancellationWithoutRetry()
    {
        var health = new AiProviderHealthRegistry();
        var inner = new StubProvider(
            "test",
            async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return new AiCompletion("never");
            });
        var provider = new ResilientAiProvider(
            inner,
            health,
            new AiProviderResilienceOptions { AttemptTimeout = TimeSpan.FromSeconds(1) });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(CreateRequest(), cancellation.Token));

        Assert.Equal(0, health.GetSnapshot("test").TotalRetries);
    }

    private static AiRequest CreateRequest() => new(
        AiPrivacyTier.LocalOllama,
        "test",
        "model",
        "answer",
        "query");

    private sealed class StubProvider : IAiProvider
    {
        private readonly Func<AiRequest, int, AiCompletion> _sync;
        private readonly Func<AiRequest, CancellationToken, Task<AiCompletion>>? _async;
        private int _attempts;

        public StubProvider(string providerKey, Func<AiRequest, int, AiCompletion> handler)
        {
            ProviderKey = providerKey;
            _sync = handler;
        }

        public StubProvider(string providerKey, Func<AiRequest, CancellationToken, Task<AiCompletion>> handler)
        {
            ProviderKey = providerKey;
            _sync = (_, _) => throw new InvalidOperationException();
            _async = handler;
        }

        public string ProviderKey { get; }

        public bool IsLocalOnly => true;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref _attempts);
            return _async is null
                ? Task.FromResult(_sync(request, attempt))
                : _async(request, cancellationToken);
        }
    }
}
