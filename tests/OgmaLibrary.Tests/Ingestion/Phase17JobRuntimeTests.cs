using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;

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

    public void Dispose() => _fixture.Dispose();
}
