using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Phase 15 OCR worker scheduling tests.</summary>
public sealed class OcrWorkerTests
{
    [Fact]
    public async Task OcrWorker_StartAsync_PollsProcessor()
    {
        var processor = new RecordingOcrJobProcessor();
        var worker = new OcrWorker(processor);

        await worker.StartAsync(CancellationToken.None);
        await processor.FirstPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, processor.Calls);
    }

    private sealed class RecordingOcrJobProcessor : IOcrJobProcessor
    {
        public TaskCompletionSource FirstPoll { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            FirstPoll.TrySetResult();
            return Task.FromResult(false);
        }
    }
}
