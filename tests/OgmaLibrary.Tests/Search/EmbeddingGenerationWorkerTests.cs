using OgmaLibrary.Application.Search;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 background worker scheduling tests.
/// </summary>
public sealed class EmbeddingGenerationWorkerTests
{
    [Fact]
    public async Task EmbeddingGenerationWorker_StartAsync_PollsGenerationService()
    {
        var generation = new RecordingEmbeddingGenerationService();
        var worker = new EmbeddingGenerationWorker(generation);

        await worker.StartAsync(CancellationToken.None);
        await generation.FirstPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, generation.Calls);
        Assert.Equal(16, generation.LastMaxChunks);
    }

    private sealed class RecordingEmbeddingGenerationService : IEmbeddingGenerationService
    {
        public TaskCompletionSource FirstPoll { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public int LastMaxChunks { get; private set; }

        public Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
            int maxChunks,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastMaxChunks = maxChunks;
            FirstPoll.TrySetResult();
            return Task.FromResult(new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: false));
        }
    }
}
