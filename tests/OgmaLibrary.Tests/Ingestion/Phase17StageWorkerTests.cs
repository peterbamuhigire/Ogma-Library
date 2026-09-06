using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 17 queue-backed search and embedding worker checks.</summary>
public sealed class Phase17StageWorkerTests
{
    [Fact]
    public async Task SearchWorker_ClaimsFtsJob_IndexesBookAndCompletesLease()
    {
        var runtime = new RecordingJobRuntime(new JobLease(
            7,
            "FtsReindexJob",
            "book-1",
            "{\"source\":\"OCR\"}",
            1,
            "test-worker",
            DateTimeOffset.UtcNow.AddMinutes(5)));
        var pipeline = new RecordingExtractionPipeline();
        var worker = new SearchExtractionWorker(pipeline, runtime);

        await worker.StartAsync(CancellationToken.None);
        await pipeline.BookIndexed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal("book-1", pipeline.LastBookId);
        await runtime.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(7, runtime.CompletedJobId);
    }

    [Fact]
    public async Task EmbeddingWorker_ClaimsEmbeddingJob_ProcessesBatchAndCompletesLease()
    {
        var runtime = new RecordingJobRuntime(new JobLease(
            8,
            "EmbeddingJob",
            "book-2",
            "{\"source\":\"search-extraction\"}",
            1,
            "test-worker",
            DateTimeOffset.UtcNow.AddMinutes(5)));
        var generation = new RecordingEmbeddingGenerationService();
        var worker = new EmbeddingGenerationWorker(generation, runtime);

        await worker.StartAsync(CancellationToken.None);
        await generation.BatchGenerated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(16, generation.LastMaxChunks);
        await runtime.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(8, runtime.CompletedJobId);
    }

    private sealed class RecordingExtractionPipeline : IExtractionPipelineService
    {
        public TaskCompletionSource BookIndexed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? LastBookId { get; private set; }

        public Task<ExtractionBookResult> IndexBookAsync(
            string bookId,
            CancellationToken cancellationToken)
        {
            LastBookId = bookId;
            BookIndexed.TrySetResult();
            return Task.FromResult(new ExtractionBookResult(bookId, true, 1, 0, 0, 1, null));
        }

        public Task<ExtractionBatchResult> IndexNextBatchAsync(
            int maxBooks,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0));
    }

    private sealed class RecordingEmbeddingGenerationService : IEmbeddingGenerationService
    {
        public TaskCompletionSource BatchGenerated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int LastMaxChunks { get; private set; }

        public Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(
            int maxChunks,
            CancellationToken cancellationToken)
        {
            LastMaxChunks = maxChunks;
            BatchGenerated.TrySetResult();
            return Task.FromResult(new EmbeddingGenerationBatchResult(1, 1, 0, 0, false));
        }
    }

    private sealed class RecordingJobRuntime : IJobRuntimeService
    {
        private JobLease? _nextLease;

        public RecordingJobRuntime(JobLease lease) => _nextLease = lease;

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long CompletedJobId { get; private set; }

        public Task<JobLease?> ClaimNextAsync(
            IReadOnlyCollection<string> jobTypes,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Interlocked.Exchange(ref _nextLease, null));

        public Task CompleteAsync(
            long jobId,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            CompletedJobId = jobId;
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task RenewAsync(
            long jobId,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task FailAsync(
            long jobId,
            string workerId,
            JobFailure failure,
            int maxAttempts = 3,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The queue-backed test job should complete.");

        public Task CancelPendingAsync(
            long jobId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RetryFailedAsync(
            long jobId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(
            int recentJobLimit = 100,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
