using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 6 acceptance tests for leased stage state transitions.</summary>
public sealed class Phase06ProcessingStateTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly ProcessingStateService _service;
    private readonly LibraryRootId _rootId = new("01PH06ROOT0000000000000001");

    public Phase06ProcessingStateTests()
    {
        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = _rootId.Value,
            DisplayName = "Phase 6 root",
            CanonicalLocator = Path.GetTempPath(),
            RootStatus = 0,
            PermissionStatus = 1,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.SaveChanges();
        _service = new ProcessingStateService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ClaimCompleteAndFinalize_ProducesDurableSuccess()
    {
        ScanSessionDescriptor session = await _service.StartSessionAsync(_rootId);
        long firstId = await _service.EnqueueStageAsync(session.Id, "Discovery", "file-a");
        long sameId = await _service.EnqueueStageAsync(session.Id, "Discovery", "file-a");
        Assert.Equal(firstId, sameId);

        StageExecutionLease? lease = await _service.ClaimNextAsync(
            "Discovery", "worker-a", TimeSpan.FromMinutes(1));
        Assert.NotNull(lease);
        Assert.Equal(1, lease.Attempt);
        Assert.Null(await _service.ClaimNextAsync("Discovery", "worker-b", TimeSpan.FromMinutes(1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CompleteStageAsync(
            lease.Id, "worker-b"));
        await _service.CompleteStageAsync(lease.Id, "worker-a");

        ScanSessionDescriptor completed = await _service.FinalizeSessionAsync(session.Id);
        Assert.Equal(ScanSessionStatus.Completed, completed.Status);
        Assert.Equal(1, completed.TotalStages);
        Assert.Equal(1, completed.CompletedStages);
    }

    [Fact]
    public async Task RetryableFailure_ReclaimsAfterDelay_AndTerminalFailureFailsSession()
    {
        ScanSessionDescriptor session = await _service.StartSessionAsync(_rootId);
        long stageId = await _service.EnqueueStageAsync(session.Id, "PdfValidation", "file-b");
        StageExecutionLease lease = (await _service.ClaimNextAsync(
            "PdfValidation", "worker-a", TimeSpan.FromMinutes(1)))!;

        await _service.FailStageAsync(
            stageId,
            "worker-a",
            new StageFailure("invalid_pdf", "The file is not a valid PDF.", Retryable: true),
            maxAttempts: 2);
        StageExecutionRow row = await _context.StageExecutions.SingleAsync();
        Assert.Equal(StageExecutionStatus.RetryableFailure, (StageExecutionStatus)row.Status);
        row.NextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync();

        StageExecutionLease retry = (await _service.ClaimNextAsync(
            "PdfValidation", "worker-b", TimeSpan.FromMinutes(1)))!;
        Assert.Equal(2, retry.Attempt);
        await _service.FailStageAsync(
            stageId,
            "worker-b",
            new StageFailure("invalid_pdf", "The file is not a valid PDF.", Retryable: true),
            maxAttempts: 2);

        ScanSessionDescriptor failed = await _service.FinalizeSessionAsync(session.Id);
        Assert.Equal(ScanSessionStatus.Failed, failed.Status);
        Assert.Equal(1, failed.FailedStages);
    }

    [Fact]
    public async Task ExpiredLease_IsReturnedToQueue_AndCancellationPreservesHistory()
    {
        ScanSessionDescriptor session = await _service.StartSessionAsync(_rootId);
        long activeId = await _service.EnqueueStageAsync(session.Id, "Extraction", "file-c");
        await _service.EnqueueStageAsync(session.Id, "Embedding", "file-c");
        StageExecutionLease active = (await _service.ClaimNextAsync(
            "Extraction", "crashed-worker", TimeSpan.FromMinutes(1)))!;
        StageExecutionRow activeRow = await _context.StageExecutions
            .SingleAsync(row => row.StageExecutionId == active.Id);
        activeRow.LeaseExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync();

        Assert.Equal(1, await _service.RecoverExpiredLeasesAsync());
        StageExecutionLease recovered = (await _service.ClaimNextAsync(
            "Extraction", "replacement-worker", TimeSpan.FromMinutes(1)))!;
        Assert.Equal(2, recovered.Attempt);

        await _service.RequestCancellationAsync(session.Id);
        Assert.Equal(StageExecutionStatus.Cancelled, (StageExecutionStatus)(
            await _context.StageExecutions.SingleAsync(row => row.StageName == "Embedding")).Status);
        await _service.CompleteStageAsync(recovered.Id, "replacement-worker");
        ScanSessionDescriptor cancelled = await _service.FinalizeSessionAsync(session.Id);
        Assert.Equal(ScanSessionStatus.Cancelled, cancelled.Status);
        Assert.Equal(2, await _context.StageExecutions.CountAsync());
    }
}
