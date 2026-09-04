using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using System.Text.Json;

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

    public void Dispose() => _fixture.Dispose();
}
