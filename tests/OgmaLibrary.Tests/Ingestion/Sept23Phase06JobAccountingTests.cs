using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Workers;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Sept-23 Phase 06 (T06.1-T06.3, T06.7): honest job accounting. Missing capabilities park
/// jobs without consuming attempts, shutdown and crash recovery never burn attempts,
/// retries back off exponentially, and a dead owner's lease is reclaimed at once.
/// </summary>
public sealed class Sept23Phase06JobAccountingTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly JobRuntimeService _runtime;

    public Sept23Phase06JobAccountingTests()
    {
        _runtime = new JobRuntimeService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CapabilityMissing_ParksJobWithoutConsumingAttempt_AndResumeRequeuesIt()
    {
        long id = await AddJobAsync("EmbeddingJob");
        JobLease lease = (await _runtime.ClaimNextAsync(["EmbeddingJob"], "w", TimeSpan.FromMinutes(1)))!;

        await _runtime.FailAsync(
            lease.JobId,
            "w",
            JobFailure.CapabilityMissing("embedding_provider_unavailable", JobCapabilities.SemanticEmbeddings));

        JobRow parked = await Job(id);
        Assert.Equal((int)JobRuntimeStatus.WaitingForCapability, parked.Status);
        Assert.Equal(0, parked.RetryCount);
        Assert.Equal(JobCapabilities.SemanticEmbeddings, parked.WaitingCapability);
        Assert.Null(await _runtime.ClaimNextAsync(["EmbeddingJob"], "w", TimeSpan.FromMinutes(1)));

        JobRuntimeMetrics metrics = await _runtime.GetMetricsAsync();
        Assert.Equal(1, metrics.WaitingForCapabilityCount);
        Assert.Equal(0, metrics.FailedCount);
        Assert.Equal(0, metrics.TotalAttempts);

        Assert.Equal(1, await _runtime.ResumeWaitingAsync(JobCapabilities.SemanticEmbeddings));
        JobLease? resumed = await _runtime.ClaimNextAsync(["EmbeddingJob"], "w", TimeSpan.FromMinutes(1));
        Assert.NotNull(resumed);
        Assert.Equal(1, resumed!.Attempt);
    }

    [Fact]
    public async Task Release_OnShutdown_ReturnsJobWithoutConsumingAttempt()
    {
        long id = await AddJobAsync("ThumbnailGeneration");
        for (int restart = 0; restart < 5; restart++)
        {
            JobLease lease = (await _runtime.ClaimNextAsync(["ThumbnailGeneration"], "w", TimeSpan.FromMinutes(1)))!;
            await _runtime.ReleaseAsync(lease.JobId, "w");
        }

        JobRow row = await Job(id);
        Assert.Equal((int)JobRuntimeStatus.Pending, row.Status);
        Assert.Equal(0, row.RetryCount);
        Assert.Equal(5, row.RequeueCount);
        Assert.Null(row.NextAttemptUtc);
    }

    [Fact]
    public async Task CrashRecovery_FiveRestarts_LeaveRetryCountUnchanged()
    {
        long id = await AddJobAsync("MetadataExtraction");
        var recovery = new JobRecoveryService(_context);
        int deadPid = DeadProcessId();
        for (int restart = 0; restart < 5; restart++)
        {
            JobLease lease = (await _runtime.ClaimNextAsync(["MetadataExtraction"], "w", TimeSpan.FromMinutes(5)))!;
            JobRow running = await Job(lease.JobId);
            running.LeaseOwnerPid = deadPid; // the process crashed mid-job
            await _context.SaveChangesAsync();

            Assert.Equal(1, await recovery.RecoverAsync());
        }

        JobRow row = await Job(id);
        Assert.Equal((int)JobRuntimeStatus.Pending, row.Status);
        Assert.Equal(0, row.RetryCount);
        Assert.Equal(5, row.RequeueCount);
    }

    [Fact]
    public async Task CrashRecovery_PoisonJob_StillReachesTerminalAttemptBudget()
    {
        long id = await AddJobAsync("SpineGeneration");
        var recovery = new JobRecoveryService(_context);
        int deadPid = DeadProcessId();
        for (int crash = 0; crash < JobRetryPolicy.MaxFreeRequeues + 3; crash++)
        {
            JobLease lease = (await _runtime.ClaimNextAsync(["SpineGeneration"], "w", TimeSpan.FromMinutes(5)))!;
            JobRow running = await Job(lease.JobId);
            running.LeaseOwnerPid = deadPid;
            await _context.SaveChangesAsync();
            await recovery.RecoverAsync();
        }

        Assert.Equal(3, (await Job(id)).RetryCount);
    }

    [Fact]
    public async Task DeadOwnerLease_IsReclaimedImmediately_NotAfterExpiry()
    {
        long id = await AddJobAsync("MetadataExtraction");
        JobLease lease = (await _runtime.ClaimNextAsync(["MetadataExtraction"], "crashed", TimeSpan.FromMinutes(5)))!;
        long other = await AddJobAsync("SearchExtraction");
        JobRow running = await Job(lease.JobId);
        running.LeaseOwnerPid = DeadProcessId();
        await _context.SaveChangesAsync();

        // The metadata-index resource group allows one running job; the dead owner's lease
        // must not block it (before Phase 06 it did for the whole 5-minute lease).
        JobLease? next = await _runtime.ClaimNextAsync(["SearchExtraction"], "alive", TimeSpan.FromMinutes(5));
        Assert.NotNull(next);
        Assert.Equal(other, next!.JobId);
        Assert.Equal(1, await _runtime.RecoverExpiredAsync());
        Assert.Equal((int)JobRuntimeStatus.Pending, (await Job(id)).Status);
    }

    [Fact]
    public async Task RetryableFailure_BacksOffExponentially_AndStopsAfterThreeRealAttempts()
    {
        long id = await AddJobAsync("ThumbnailGeneration");
        TimeSpan[] nominal = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            JobLease lease = (await _runtime.ClaimNextAsync(["ThumbnailGeneration"], "w", TimeSpan.FromMinutes(1)))!;
            Assert.Equal(attempt, lease.Attempt);
            DateTimeOffset before = DateTimeOffset.UtcNow;
            await _runtime.FailAsync(lease.JobId, "w", new JobFailure("io_timeout", "temporary", Retryable: true));
            JobRow row = await Job(id);
            if (attempt < 3)
            {
                Assert.Equal((int)JobRuntimeStatus.Pending, row.Status);
                TimeSpan delay = row.NextAttemptUtc!.Value - before;
                Assert.InRange(delay.TotalSeconds, nominal[attempt - 1].TotalSeconds * 0.79, nominal[attempt - 1].TotalSeconds * 1.21 + 1);
                row.NextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                await _context.SaveChangesAsync();
            }
            else
            {
                Assert.Equal((int)JobRuntimeStatus.Failed, row.Status);
                Assert.Null(row.NextAttemptUtc);
            }
        }
    }

    [Fact]
    public async Task PermanentAndCancelledFailures_AreNotRetried()
    {
        long permanent = await AddJobAsync("MetadataExtraction");
        JobLease lease = (await _runtime.ClaimNextAsync(["MetadataExtraction"], "w", TimeSpan.FromMinutes(1)))!;
        await _runtime.FailAsync(lease.JobId, "w", new JobFailure("not_a_pdf", null, Retryable: true, Kind: JobFailureKind.Permanent));
        Assert.Equal((int)JobRuntimeStatus.Failed, (await Job(permanent)).Status);

        long cancelled = await AddJobAsync("ThumbnailGeneration");
        lease = (await _runtime.ClaimNextAsync(["ThumbnailGeneration"], "w", TimeSpan.FromMinutes(1)))!;
        await _runtime.FailAsync(lease.JobId, "w", JobFailure.Cancelled("book_removed"));
        Assert.Equal((int)JobRuntimeStatus.Cancelled, (await Job(cancelled)).Status);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    [InlineData(4, 600)]
    [InlineData(9, 600)]
    public void RetrySchedule_IsExponentialWithTwentyPercentJitter(int completedAttempts, int nominalSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(nominalSeconds), JobRetryPolicy.NominalDelay(completedAttempts));
        Assert.Equal(nominalSeconds * 0.8, JobRetryPolicy.Delay(completedAttempts, 0).TotalSeconds, 3);
        Assert.Equal(nominalSeconds * 1.2, JobRetryPolicy.Delay(completedAttempts, 1).TotalSeconds, 3);
    }

    [Fact]
    public async Task ReRegistration_OfFailedJob_ResetsAttemptsInsteadOfAddingOne()
    {
        var registration = new BookRegistrationService(_context);
        string temp = Path.Combine(Path.GetTempPath(), $"ogma-p06-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(temp, "%PDF-1.4\n%%EOF");
        try
        {
            string bookId = await registration.RegisterAsync(new DiscoveredFile(temp, "a.pdf", 10, 1), "abc123");
            foreach (JobRow job in await _context.Jobs.Where(job => job.BookId == bookId).ToListAsync())
            {
                job.Status = (int)JobRuntimeStatus.Failed;
                job.RetryCount = 3;
            }

            await _context.SaveChangesAsync();
            await registration.UpdateFilePathAsync(bookId, new DiscoveredFile(temp, "a.pdf", 10, 1), "abc123");

            List<JobRow> jobs = await _context.Jobs.AsNoTracking().Where(job => job.BookId == bookId).ToListAsync();
            Assert.Contains(jobs, job => job.Status == (int)JobRuntimeStatus.Pending);
            Assert.All(
                jobs.Where(job => job.Status == (int)JobRuntimeStatus.Pending),
                job => Assert.Equal(0, job.RetryCount));
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private async Task<long> AddJobAsync(string type)
    {
        var row = new JobRow
        {
            JobType = type,
            IdempotencyKey = $"phase06-{Guid.NewGuid():N}",
            Status = (int)JobRuntimeStatus.Pending,
        };
        _context.Jobs.Add(row);
        await _context.SaveChangesAsync();
        return row.JobId;
    }

    private async Task<JobRow> Job(long id)
    {
        JobRow row = await _context.Jobs.SingleAsync(job => job.JobId == id);
        await _context.Entry(row).ReloadAsync();
        return row;
    }

    private static int DeadProcessId()
    {
        using Process process = Process.Start(new ProcessStartInfo(
            OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            OperatingSystem.IsWindows() ? "/c exit 0" : "-c true")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        process.WaitForExit();
        return process.Id;
    }
}
