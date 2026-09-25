using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Workers;

/// <summary>
/// Re-queues background jobs that were left in the <c>Running</c> state (Status=1)
/// when the application crashed or was terminated mid-scan (NFR-OGMA-009).
/// Called once at startup before the <see cref="BookIngestionWorker"/> begins.
/// </summary>
public sealed class JobRecoveryService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="JobRecoveryService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal JobRecoveryService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="JobRecoveryService"/>.
    /// </summary>
    public JobRecoveryService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns interrupted <c>Running</c> jobs to <c>Pending</c>: unleased or expired leases
    /// and leases whose owner process is no longer alive (Sept-23 Phase 06, T06.7), so a crash
    /// never blocks a resource group until the lease expires. Recovery is not an attempt
    /// (T06.2): the interrupted claim is refunded and counted in <c>RequeueCount</c>, except
    /// after <see cref="JobRetryPolicy.MaxFreeRequeues"/> interruptions, when the claim stands
    /// so a job that keeps killing its process still reaches a terminal state. A live lease
    /// of another process is never stolen. An audit event is appended per job (NFR-OGMA-009).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of jobs recovered.</returns>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<JobRow> running = await context.Jobs
            .Where(job => job.Status == (int)JobRuntimeStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<JobRow> stuck = running
            .Where(job => job.LeaseExpiresUtc is null ||
                          job.LeaseExpiresUtc < now ||
                          !LeaseOwnerIdentity.IsAlive(job.LeaseOwnerPid, job.LeaseOwnerStartTicks))
            .ToList();

        foreach (JobRow job in stuck)
        {
            job.Status = (int)JobRuntimeStatus.Pending;
            if (job.RequeueCount < JobRetryPolicy.MaxFreeRequeues)
            {
                // An interrupted claim is not a real attempt (T06.2).
                job.RetryCount = Math.Max(0, job.RetryCount - 1);
            }

            job.RequeueCount += 1;
            job.StartedUtc = null;
            job.LeaseOwner = null;
            job.LeaseExpiresUtc = null;
            job.LeaseOwnerPid = null;
            job.LeaseOwnerStartTicks = null;
            job.NextAttemptUtc = now;
            job.FailureCode = "startup_recovery";

            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "JobRecovered",
                EntityId = job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                EntityType = "Job",
                AfterJson = $"{{\"retryCount\":{job.RetryCount},\"requeueCount\":{job.RequeueCount}}}",
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });
        }

        if (stuck.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return stuck.Count;
    }
}
