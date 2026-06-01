using OgmaLibrary.Application.Search;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 background worker scheduling tests.
/// </summary>
public sealed class SearchExtractionWorkerTests
{
    [Fact]
    public async Task SearchExtractionWorker_StartAsync_PollsExtractionPipeline()
    {
        var pipeline = new RecordingExtractionPipeline();
        var worker = new SearchExtractionWorker(pipeline);

        await worker.StartAsync(CancellationToken.None);
        await pipeline.FirstPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, pipeline.Calls);
        Assert.Equal(3, pipeline.LastMaxBooks);
    }

    private sealed class RecordingExtractionPipeline : IExtractionPipelineService
    {
        public TaskCompletionSource FirstPoll { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public int LastMaxBooks { get; private set; }

        public Task<ExtractionBookResult> IndexBookAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBookResult(bookId, true, 0, 0, 0, 0, null));

        public Task<ExtractionBatchResult> IndexNextBatchAsync(
            int maxBooks,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastMaxBooks = maxBooks;
            FirstPoll.TrySetResult();
            return Task.FromResult(new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0));
        }
    }
}
