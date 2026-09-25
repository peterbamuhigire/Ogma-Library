using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Workers;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Sept-23 Phase 06 (T06.5, K26) fault injection: every hosted worker survives an exception
/// in its claim or failure path, backs off and keeps processing. Before this phase one
/// exception from <c>ClaimNextAsync</c> or <c>FailAsync</c> ended <see cref="BookIngestionWorker"/>
/// (covers, spines and metadata) until the application restarted.
/// </summary>
public sealed class Sept23Phase06WorkerResilienceTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);
    private readonly string _dbPath;
    private readonly FileContextFactory _factory;

    public Sept23Phase06WorkerResilienceTests()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _dbPath = dbPath;
        using (context)
        {
            context.Database.EnsureCreated();
            context.Books.Add(new BookRow { BookId = "BOOK-1", Sha256Hash = new string('a', 64), Status = 0 });
            context.Books.Add(new BookRow { BookId = "BOOK-2", Sha256Hash = new string('b', 64), Status = 0 });
            context.SaveChanges();
        }

        _factory = new FileContextFactory(dbPath);
    }

    public void Dispose() => CatalogueTestHelper.DeleteTempDb(_dbPath);

    [Fact]
    public async Task BookIngestionWorker_SurvivesDbBusyOnClaim_AndExceptionInFail_ThenCompletesCovers()
    {
        var runtime = new ScriptedRuntime(
            claimFailures: 2,
            leases:
            [
                Lease(1, "ThumbnailGeneration", "BOOK-1"),
                Lease(2, "ThumbnailGeneration", "BOOK-2"),
            ])
        {
            ThrowOnFail = true,
        };
        var thumbnails = new ScriptedThumbnails(failBookId: "BOOK-1");
        var worker = new BookIngestionWorker(
            _factory,
            runtime,
            new NoopMetadata(),
            thumbnails,
            new NoopSpines(),
            new NoopEnrichment(),
            logger: NullLogger<BookIngestionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await runtime.Completed.Task.WaitAsync(Wait);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, runtime.ClaimExceptions);
        Assert.Contains(1L, runtime.FailAttempts);
        Assert.Contains(2L, runtime.CompletedJobs);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task BookIngestionWorker_Shutdown_ReleasesLeaseInsteadOfFailing()
    {
        var runtime = new ScriptedRuntime(0, [Lease(1, "ThumbnailGeneration", "BOOK-1")]);
        var thumbnails = new ScriptedThumbnails(failBookId: null) { BlockUntilCancelled = true };
        var worker = new BookIngestionWorker(
            _factory,
            runtime,
            new NoopMetadata(),
            thumbnails,
            new NoopSpines(),
            new NoopEnrichment());

        await worker.StartAsync(CancellationToken.None);
        await thumbnails.Started.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None);

        Assert.Contains(1L, runtime.ReleasedJobs);
        Assert.Empty(runtime.FailAttempts);
    }

    [Fact]
    public async Task SearchExtractionWorker_SurvivesClaimException()
    {
        var runtime = new ScriptedRuntime(1, [Lease(7, "SearchExtraction", "BOOK-1")]);
        var worker = new SearchExtractionWorker(new PassingPipeline(), runtime);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await runtime.Completed.Task.WaitAsync(Wait);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(1, runtime.ClaimExceptions);
        Assert.Contains(7L, runtime.CompletedJobs);
    }

    [Fact]
    public async Task EmbeddingGenerationWorker_ParksJobsWhenProviderIsMissing_AfterClaimException()
    {
        var runtime = new ScriptedRuntime(1, [Lease(8, "EmbeddingJob", "BOOK-1")]);
        var worker = new EmbeddingGenerationWorker(new UnavailableEmbeddings(), runtime);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await runtime.Failed.Task.WaitAsync(Wait);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        JobFailure failure = runtime.Failures.Single();
        Assert.Equal(JobFailureKind.CapabilityMissing, failure.EffectiveKind);
        Assert.Equal(JobCapabilities.SemanticEmbeddings, failure.Capability);
    }

    [Fact]
    public async Task OcrWorker_SurvivesProcessorExceptions()
    {
        var processor = new FlakyOcrProcessor(failures: 2);
        var worker = new OcrWorker(processor);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await processor.Succeeded.Task.WaitAsync(Wait);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.True(processor.Calls >= 3);
    }

    [Fact]
    public async Task ResilientJobLoop_BacksOffExponentially_AndResetsAfterSuccess()
    {
        var delays = new List<TimeSpan>();
        using var stop = new CancellationTokenSource();
        int call = 0;
        await ResilientJobLoop.RunAsync(
            "test",
            _ =>
            {
                call++;
                if (call is 1 or 2 or 3 or 5)
                {
                    throw new InvalidOperationException("injected");
                }

                if (call == 6)
                {
                    stop.Cancel();
                }

                return Task.FromResult(true);
            },
            TimeSpan.FromSeconds(9),
            NullLogger.Instance,
            stop.Token,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1)],
            delays);
        Assert.Equal(TimeSpan.FromSeconds(30), ResilientJobLoop.ErrorDelay(20));
    }

    private static JobLease Lease(long id, string type, string bookId) =>
        new(id, type, bookId, @"C:\not-used\book.pdf", 1, "w", DateTimeOffset.UtcNow.AddMinutes(5));

    private sealed class FileContextFactory(string dbPath) : IDbContextFactory<CatalogueDbContext>
    {
        public CatalogueDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options);
    }

    private sealed class ScriptedRuntime(int claimFailures, IReadOnlyList<JobLease> leases) : IJobRuntimeService
    {
        private readonly ConcurrentQueue<JobLease> _leases = new(leases);
        private int _claimFailuresLeft = claimFailures;

        public bool ThrowOnFail { get; init; }

        public int ClaimExceptions { get; private set; }

        public ConcurrentBag<long> CompletedJobs { get; } = [];

        public ConcurrentBag<long> FailAttempts { get; } = [];

        public ConcurrentBag<long> ReleasedJobs { get; } = [];

        public ConcurrentBag<JobFailure> Failures { get; } = [];

        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<JobLease?> ClaimNextAsync(
            IReadOnlyCollection<string> jobTypes,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (_claimFailuresLeft > 0)
            {
                _claimFailuresLeft--;
                ClaimExceptions++;
                throw new InvalidOperationException("SQLite Error 5: 'database is locked'.");
            }

            return Task.FromResult(_leases.TryDequeue(out JobLease? lease) ? lease : null);
        }

        public Task CompleteAsync(long jobId, string workerId, CancellationToken cancellationToken = default)
        {
            CompletedJobs.Add(jobId);
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task RenewAsync(long jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FailAsync(
            long jobId,
            string workerId,
            JobFailure failure,
            int maxAttempts = 3,
            CancellationToken cancellationToken = default)
        {
            FailAttempts.Add(jobId);
            Failures.Add(failure);
            Failed.TrySetResult();
            if (ThrowOnFail)
            {
                throw new InvalidOperationException("injected failure while recording the failure");
            }

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(long jobId, string workerId, CancellationToken cancellationToken = default)
        {
            ReleasedJobs.Add(jobId);
            return Task.CompletedTask;
        }

        public Task CancelPendingAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RetryFailedAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(int recentJobLimit = 100, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ScriptedThumbnails(string? failBookId) : IThumbnailService
    {
        public bool BlockUntilCancelled { get; init; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<(bool Success, string? ErrorMessage)> GenerateCoverAsync(
            string bookId,
            string contentHash,
            string absoluteFilePath,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            if (BlockUntilCancelled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return bookId == failBookId ? (false, "injected") : (true, null);
        }

        public Task<(bool Success, string? ErrorMessage)> GenerateCoverVariantAsync(
            string bookId,
            string contentHash,
            string absoluteFilePath,
            string variant,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class NoopSpines : ISpineService
    {
        public Task<(bool Success, string? ErrorMessage)> GenerateSpineAsync(
            string bookId,
            string contentHash,
            string absoluteFilePath,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, string?)>((true, null));

        public Task<(bool Success, string? ErrorMessage)> GenerateSpineVariantAsync(
            string bookId,
            string contentHash,
            string absoluteFilePath,
            string variant,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class NoopMetadata : IMetadataExtractionService
    {
        public Task<(bool Success, string? ErrorMessage)> ExtractAsync(
            string bookId,
            string absoluteFilePath,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class NoopEnrichment : IBookMetadataEnrichmentService
    {
        public Task<(bool Success, string? ErrorMessage)> EnrichAsync(
            string bookId,
            string? absoluteFilePath,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class PassingPipeline : IExtractionPipelineService
    {
        public Task<ExtractionBookResult> IndexBookAsync(string bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBookResult(bookId, true, 1, 0, 0, 1, null));

        public Task<ExtractionBatchResult> IndexNextBatchAsync(int maxBooks, CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractionBatchResult(0, 0, 0, 0, 0, 0, 0));
    }

    private sealed class UnavailableEmbeddings : IEmbeddingGenerationService
    {
        public Task<EmbeddingGenerationBatchResult> GenerateNextBatchAsync(int maxChunks, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingGenerationBatchResult(0, 0, 0, 0, ProviderUnavailable: true));
    }

    private sealed class FlakyOcrProcessor(int failures) : IOcrJobProcessor
    {
        private int _failuresLeft = failures;

        public int Calls { get; private set; }

        public TaskCompletionSource Succeeded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (_failuresLeft-- > 0)
            {
                throw new IOException("injected OCR failure");
            }

            Succeeded.TrySetResult();
            return Task.FromResult(false);
        }
    }
}
