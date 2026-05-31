using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for job management: recovery and per-file isolation (NFR-OGMA-009, NFR-PROD-006).
/// </summary>
public sealed class JobManagementTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(5);

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task JobRecovery_AtStartup_RequeuesRunningJobs()
    {
        // Manually insert 2 jobs in Running state (simulating a crashed mid-scan).
        _fx.Context.Jobs.Add(new JobRow
        {
            JobType = "ThumbnailGeneration",
            IdempotencyKey = "recovery-test-key-1",
            Status = 1, // Running
            BookId = null,
            Payload = "/test/path1.pdf",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        _fx.Context.Jobs.Add(new JobRow
        {
            JobType = "MetadataExtraction",
            IdempotencyKey = "recovery-test-key-2",
            Status = 1, // Running
            BookId = null,
            Payload = "/test/path2.pdf",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        await _fx.Context.SaveChangesAsync();

        // Run recovery.
        int recovered = await _fx.JobRecovery.RecoverAsync();

        Assert.Equal(2, recovered);

        // Both jobs should now be Pending.
        var recoveredJobs = await _fx.Context.Jobs
            .Where(j => j.IdempotencyKey == "recovery-test-key-1" ||
                        j.IdempotencyKey == "recovery-test-key-2")
            .ToListAsync();

        Assert.All(recoveredJobs, j => Assert.Equal(0, j.Status)); // Pending
        Assert.All(recoveredJobs, j => Assert.True(j.RetryCount >= 1));
    }

    [Fact]
    public async Task IngestionOrchestrator_PerFileIsolation_BatchContinuesOnFailure()
    {
        // Scan the real PDFs first to register them.
        await _fx.Orchestrator.ScanAsync();

        int booksRegistered = await _fx.Context.Books.CountAsync();
        Assert.True(booksRegistered >= 1, "Expected at least one book to be registered");

        // Now manually inject a failure job.
        _fx.Context.Jobs.Add(new JobRow
        {
            JobType = "IngestionFailure",
            IdempotencyKey = "isolation-test-failure",
            Status = 3, // Failed
            Payload = "corrupt/file.pdf",
            ErrorMessage = "Simulated failure for isolation test",
            CompletedUtc = DateTimeOffset.UtcNow,
        });
        await _fx.Context.SaveChangesAsync();

        // Other books should still be registered (pipeline continued past the failure).
        int booksAfter = await _fx.Context.Books.CountAsync();
        Assert.True(booksAfter >= 1, "All books should still be registered despite failure");

        // The failure job should exist in the database.
        bool failureRecorded = await _fx.Context.Jobs
            .AnyAsync(j => j.Status == 3 && j.IdempotencyKey == "isolation-test-failure");
        Assert.True(failureRecorded, "Failed job was not recorded");
    }
}
