using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Restart-style load evidence for the durable Phase 17 job runtime. The test
/// disposes contexts to model worker loss; it does not claim an OS process-kill
/// or cross-machine soak test.
/// </summary>
public sealed class Phase17RuntimeRecoveryLoadTests
{
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task RestartRecovery_ReclaimsOrphanedLease_AndDrainsBoundedQueue()
    {
        (CatalogueDbContext firstContext, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            firstContext.Database.EnsureCreated();
            const int jobCount = 64;
            firstContext.Jobs.AddRange(Enumerable.Range(0, jobCount).Select(index => new JobRow
            {
                JobType = "MetadataExtraction",
                IdempotencyKey = $"phase17-restart-load-{index:000}",
                Status = (int)JobRuntimeStatus.Pending,
            }));
            await firstContext.SaveChangesAsync();

            JobLease orphanedLease = (await new JobRuntimeService(firstContext).ClaimNextAsync(
                ["MetadataExtraction"],
                "worker-before-restart",
                TimeSpan.FromMinutes(1)))!;

            // Simulate the worker disappearing before completion. The context
            // disposal is the durable boundary being exercised here.
            firstContext.Dispose();

            using (CatalogueDbContext recoveryContext = CreateContext(dbPath))
            {
                JobRow orphanedJob = await recoveryContext.Jobs
                    .SingleAsync(job => job.JobId == orphanedLease.JobId);
                orphanedJob.LeaseExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                await recoveryContext.SaveChangesAsync();

                int recovered = await new JobRuntimeService(recoveryContext).RecoverExpiredAsync();
                Assert.Equal(1, recovered);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int processed = 0;
            while (true)
            {
                using CatalogueDbContext workerContext = CreateContext(dbPath);
                JobRuntimeService runtime = new(workerContext);
                JobLease? lease = await runtime.ClaimNextAsync(
                    ["MetadataExtraction"],
                    $"worker-restart-{processed / 8:00}",
                    TimeSpan.FromMinutes(1));
                if (lease is null)
                {
                    break;
                }

                await runtime.CompleteAsync(lease.JobId, lease.LeaseOwner);
                processed++;
            }

            stopwatch.Stop();
            using CatalogueDbContext assertionContext = CreateContext(dbPath);
            Assert.Equal(jobCount, processed);
            Assert.Equal(
                jobCount,
                await assertionContext.Jobs.CountAsync(job =>
                    job.Status == (int)JobRuntimeStatus.Completed));
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Restart-style queue drain took {stopwatch.ElapsedMilliseconds} ms.");
        }
        finally
        {
            firstContext.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    private static CatalogueDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False")
            .Options;
        return new CatalogueDbContext(options);
    }
}
