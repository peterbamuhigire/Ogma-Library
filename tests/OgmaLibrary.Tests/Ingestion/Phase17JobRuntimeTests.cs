using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 17 tests for atomic job claims, ownership, retry and recovery.</summary>
public sealed class Phase17JobRuntimeTests : IDisposable
{
    private readonly IngestionTestFixture _fixture = IngestionTestFixture.Create(0);

    [Fact]
    public async Task Claim_IsExclusive_AndCompletionRequiresLeaseOwner()
    {
        _fixture.Context.Jobs.Add(new JobRow
        {
            JobType = "ThumbnailGeneration",
            IdempotencyKey = "phase17-exclusive",
            Status = (int)JobRuntimeStatus.Pending,
        });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);

        JobLease? first = await runtime.ClaimNextAsync(
            ["ThumbnailGeneration"], "worker-a", TimeSpan.FromMinutes(1));
        JobLease? second = await runtime.ClaimNextAsync(
            ["ThumbnailGeneration"], "worker-b", TimeSpan.FromMinutes(1));

        Assert.NotNull(first);
        Assert.Null(second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.CompleteAsync(
            first.JobId, "worker-b"));
        await runtime.CompleteAsync(first.JobId, "worker-a");

        JobRow row = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == first.JobId);
        Assert.Equal((int)JobRuntimeStatus.Completed, row.Status);
        Assert.Null(row.LeaseOwner);
        Assert.Null(row.LeaseExpiresUtc);
    }

    [Fact]
    public async Task RetryableFailure_SchedulesBackoff_AndTerminalFailureStopsRetry()
    {
        _fixture.Context.Jobs.Add(new JobRow
        {
            JobType = "MetadataExtraction",
            IdempotencyKey = "phase17-retry",
            Status = (int)JobRuntimeStatus.Pending,
        });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);

        JobLease first = (await runtime.ClaimNextAsync(
            ["MetadataExtraction"], "worker-a", TimeSpan.FromMinutes(1)))!;
        await runtime.FailAsync(first.JobId, "worker-a", new JobFailure("io_timeout", "temporary", true), 2);
        JobRow afterFirst = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == first.JobId);
        Assert.Equal((int)JobRuntimeStatus.Pending, afterFirst.Status);
        Assert.Equal("io_timeout", afterFirst.FailureCode);
        Assert.NotNull(afterFirst.NextAttemptUtc);

        afterFirst.NextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _fixture.Context.SaveChangesAsync();
        JobLease second = (await runtime.ClaimNextAsync(
            ["MetadataExtraction"], "worker-a", TimeSpan.FromMinutes(1)))!;
        await runtime.FailAsync(second.JobId, "worker-a", new JobFailure("bad_input", "permanent", true), 2);

        JobRow terminal = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == first.JobId);
        Assert.Equal((int)JobRuntimeStatus.Failed, terminal.Status);
        Assert.Equal("bad_input", terminal.FailureCode);
        Assert.Null(terminal.NextAttemptUtc);
    }

    [Fact]
    public async Task ExpiredLease_IsReturnedToQueue()
    {
        _fixture.Context.Jobs.Add(new JobRow
        {
            JobType = "SpineGeneration",
            IdempotencyKey = "phase17-expiry",
            Status = (int)JobRuntimeStatus.Pending,
        });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);
        JobLease claim = (await runtime.ClaimNextAsync(
            ["SpineGeneration"], "worker-a", TimeSpan.FromMinutes(1)))!;
        JobRow claimed = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == claim.JobId);
        claimed.LeaseExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _fixture.Context.SaveChangesAsync();

        Assert.Equal(1, await runtime.RecoverExpiredAsync());
        JobLease? recovered = await runtime.ClaimNextAsync(
            ["SpineGeneration"], "worker-b", TimeSpan.FromMinutes(1));

        Assert.NotNull(recovered);
        Assert.Equal("worker-b", recovered.LeaseOwner);
        Assert.Equal(2, recovered.Attempt);
    }

    [Fact]
    public async Task PoisonFailure_IsQuarantinedWithoutRetry()
    {
        _fixture.Context.Jobs.Add(new JobRow
        {
            JobType = "Unknown",
            IdempotencyKey = "phase17-poison",
            Status = (int)JobRuntimeStatus.Pending,
        });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);
        JobLease claim = (await runtime.ClaimNextAsync(
            ["Unknown"], "worker-a", TimeSpan.FromMinutes(1)))!;

        await runtime.FailAsync(
            claim.JobId,
            "worker-a",
            new JobFailure("unsupported_job", "The job type is not supported.", Retryable: false, DeadLetter: true));

        JobRow row = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == claim.JobId);
        Assert.Equal((int)JobRuntimeStatus.DeadLetter, row.Status);
        Assert.Null(row.NextAttemptUtc);
    }

    [Fact]
    public async Task CancelPending_IsDurableAuditedAndCannotMisreportActiveWork()
    {
        _fixture.Context.Jobs.AddRange(
            new JobRow
            {
                JobType = "ThumbnailGeneration",
                IdempotencyKey = "phase17-cancel-pending",
                Status = (int)JobRuntimeStatus.Pending,
            },
            new JobRow
            {
                JobType = "SpineGeneration",
                IdempotencyKey = "phase17-cancel-running",
                Status = (int)JobRuntimeStatus.Pending,
            });
        await _fixture.Context.SaveChangesAsync();
        long pendingJobId = _fixture.Context.Jobs.Single(job =>
            job.IdempotencyKey == "phase17-cancel-pending").JobId;
        _fixture.Context.ChangeTracker.Clear();
        var runtime = new JobRuntimeService(_fixture.Context);
        JobLease running = (await runtime.ClaimNextAsync(
            ["SpineGeneration"], "worker-a", TimeSpan.FromMinutes(1)))!;

        await runtime.CancelPendingAsync(pendingJobId);
        await runtime.CancelPendingAsync(pendingJobId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.CancelPendingAsync(running.JobId));

        JobRow cancelled = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == pendingJobId);
        Assert.Equal((int)JobRuntimeStatus.Cancelled, cancelled.Status);
        Assert.Equal("cancelled_by_user", cancelled.FailureCode);
        Assert.NotNull(cancelled.CompletedUtc);
        Assert.Null(cancelled.NextAttemptUtc);
        Assert.Equal(
            1,
            _fixture.Context.AuditEvents.Count(audit =>
                audit.EventType == "JobCancelled" &&
                audit.EntityId == pendingJobId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(
            (int)JobRuntimeStatus.Running,
            _fixture.Context.Jobs.Single(job => job.JobId == running.JobId).Status);
    }

    [Fact]
    public async Task JobLifecycleEvents_AreStructuredAndRedacted()
    {
        _fixture.Context.Jobs.Add(new JobRow
        {
            JobType = "MetadataExtraction",
            IdempotencyKey = "phase17-events",
            Status = (int)JobRuntimeStatus.Pending,
            Payload = "C:/private/library/book.pdf",
        });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);

        JobLease lease = (await runtime.ClaimNextAsync(
            ["MetadataExtraction"], "worker-a", TimeSpan.FromMinutes(1)))!;
        await runtime.FailAsync(
            lease.JobId,
            "worker-a",
            new JobFailure("io_timeout", "C:/private/library/book.pdf: provider detail", Retryable: false));

        string jobId = lease.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AuditEventRow[] events = _fixture.Context.AuditEvents
            .Where(audit => audit.EntityId == jobId)
            .OrderBy(audit => audit.EventId)
            .ToArray();
        Assert.Equal(["JobClaimed", "JobFailed"], events.Select(audit => audit.EventType).ToArray());
        Assert.All(events, audit =>
        {
            Assert.NotNull(audit.AfterJson);
            using JsonDocument document = JsonDocument.Parse(audit.AfterJson!);
            Assert.True(document.RootElement.TryGetProperty("jobType", out _));
            Assert.DoesNotContain("private/library", audit.AfterJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("provider detail", audit.AfterJson, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Claim_EnforcesHeavyWorkResourceGroupCapacity()
    {
        _fixture.Context.Jobs.AddRange(
            new JobRow
            {
                JobType = "OcrJob",
                IdempotencyKey = "phase17-resource-ocr",
                Status = (int)JobRuntimeStatus.Pending,
            },
            new JobRow
            {
                JobType = "PdfRender",
                IdempotencyKey = "phase17-resource-pdf",
                Status = (int)JobRuntimeStatus.Pending,
            });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);

        JobLease? first = await runtime.ClaimNextAsync(
            ["OcrJob", "PdfRender"], "worker-a", TimeSpan.FromMinutes(1));
        JobLease? second = await runtime.ClaimNextAsync(
            ["OcrJob", "PdfRender"], "worker-b", TimeSpan.FromMinutes(1));

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Metrics_ReturnRedactedStatusTotalsAndActiveLeaseCounts()
    {
        _fixture.Context.Jobs.AddRange(
            new JobRow
            {
                JobType = "MetadataExtraction",
                IdempotencyKey = "phase17-metrics-pending",
                Status = (int)JobRuntimeStatus.Pending,
                RetryCount = 2,
            },
            new JobRow
            {
                JobType = "OcrJob",
                IdempotencyKey = "phase17-metrics-running",
                Status = (int)JobRuntimeStatus.Running,
                LeaseOwner = "metrics-worker",
                LeaseExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1),
                RetryCount = 1,
            },
            new JobRow
            {
                JobType = "MetadataExtraction",
                IdempotencyKey = "phase17-metrics-completed",
                Status = (int)JobRuntimeStatus.Completed,
                RetryCount = 3,
            },
            new JobRow
            {
                JobType = "MetadataExtraction",
                IdempotencyKey = "phase17-metrics-failed",
                Status = (int)JobRuntimeStatus.Failed,
                RetryCount = 3,
            },
            new JobRow
            {
                JobType = "Unknown",
                IdempotencyKey = "phase17-metrics-dead-letter",
                Status = (int)JobRuntimeStatus.DeadLetter,
                RetryCount = 1,
            },
            new JobRow
            {
                JobType = "ThumbnailGeneration",
                IdempotencyKey = "phase17-metrics-cancelled",
                Status = (int)JobRuntimeStatus.Cancelled,
            },
            new JobRow
            {
                JobType = "OcrJob",
                IdempotencyKey = "phase17-metrics-paused",
                Status = (int)JobRuntimeStatus.Paused,
            });
        await _fixture.Context.SaveChangesAsync();

        JobRuntimeMetrics metrics = await new JobRuntimeService(_fixture.Context)
            .GetMetricsAsync();

        Assert.Equal(1, metrics.PendingCount);
        Assert.Equal(1, metrics.RunningCount);
        Assert.Equal(1, metrics.CompletedCount);
        Assert.Equal(1, metrics.FailedCount);
        Assert.Equal(1, metrics.CancelledCount);
        Assert.Equal(1, metrics.DeadLetterCount);
        Assert.Equal(1, metrics.PausedCount);
        Assert.Equal(10, metrics.TotalAttempts);
        Assert.Equal(1, metrics.ActiveByJobType["OcrJob"]);

        string diagnostics = await new JobRuntimeService(_fixture.Context)
            .ExportDiagnosticsJsonAsync();
        Assert.DoesNotContain("phase17-metrics", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("payload", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaseOwner", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deadLetterCount", diagnostics, StringComparison.Ordinal);
        Assert.Contains("cancelledCount", diagnostics, StringComparison.Ordinal);
        Assert.Contains("pausedCount", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivityActions_RetryOnlyFailedWork_AndAuditTheTransition()
    {
        _fixture.Context.Jobs.AddRange(
            new JobRow
            {
                JobType = "MetadataExtraction",
                IdempotencyKey = "phase17-activity-failed",
                Status = (int)JobRuntimeStatus.Failed,
                RetryCount = 3,
                FailureCode = "io_timeout",
                ErrorMessage = "A safe terminal message.",
                CompletedUtc = DateTimeOffset.UtcNow,
            },
            new JobRow
            {
                JobType = "Unknown",
                IdempotencyKey = "phase17-activity-dead-letter",
                Status = (int)JobRuntimeStatus.DeadLetter,
            });
        await _fixture.Context.SaveChangesAsync();
        var runtime = new JobRuntimeService(_fixture.Context);
        JobRow failed = _fixture.Context.Jobs.Single(job => job.IdempotencyKey == "phase17-activity-failed");
        JobRow deadLetter = _fixture.Context.Jobs.Single(job => job.IdempotencyKey == "phase17-activity-dead-letter");

        JobRuntimeDiagnostics snapshot = await runtime.GetDiagnosticsAsync(1);
        await runtime.RetryFailedAsync(failed.JobId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RetryFailedAsync(deadLetter.JobId));

        _fixture.Context.ChangeTracker.Clear();
        JobRow queued = await _fixture.Context.Jobs.SingleAsync(job => job.JobId == failed.JobId);
        Assert.Single(snapshot.RecentJobs);
        Assert.Equal((int)JobRuntimeStatus.Pending, queued.Status);
        Assert.Equal(3, queued.RetryCount);
        Assert.Null(queued.FailureCode);
        Assert.Null(queued.ErrorMessage);
        Assert.Null(queued.CompletedUtc);
        Assert.NotNull(queued.NextAttemptUtc);
        Assert.Equal(
            1,
            _fixture.Context.AuditEvents.Count(audit =>
                audit.EventType == "JobRetryQueued" &&
                audit.EntityId == failed.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task PausedStatusMigration_SeparatesPausedWorkFromDeadLetters()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            IMigrator migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260905090519_Phase13ProviderLookupStaleness");
            context.Jobs.AddRange(
                new JobRow
                {
                    JobType = "OcrJob",
                    IdempotencyKey = "phase17-legacy-paused-ocr",
                    Status = 5,
                },
                new JobRow
                {
                    JobType = "Enrich",
                    IdempotencyKey = "phase17-legacy-paused-enrich",
                    Status = 5,
                },
                new JobRow
                {
                    JobType = "Unknown",
                    IdempotencyKey = "phase17-existing-dead-letter",
                    Status = (int)JobRuntimeStatus.DeadLetter,
                });
            await context.SaveChangesAsync();

            await migrator.MigrateAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(
                (int)JobRuntimeStatus.Paused,
                context.Jobs.Single(job => job.JobType == "OcrJob").Status);
            Assert.Equal(
                (int)JobRuntimeStatus.Paused,
                context.Jobs.Single(job => job.JobType == "Enrich").Status);
            Assert.Equal(
                (int)JobRuntimeStatus.DeadLetter,
                context.Jobs.Single(job => job.JobType == "Unknown").Status);
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    public void Dispose() => _fixture.Dispose();
}
