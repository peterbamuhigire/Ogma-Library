using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

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
    /// Loads legacy unleased or expired jobs with <c>Status = Running</c> and
    /// resets them to <c>Status = Pending</c> with <c>RetryCount + 1</c>,
    /// appending an audit event per recovered job (NFR-OGMA-009). A valid lease
    /// is never stolen from another live process.
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
            .Where(job => job.LeaseExpiresUtc is null || job.LeaseExpiresUtc < now)
            .ToList();

        foreach (JobRow job in stuck)
        {
            job.Status = (int)JobRuntimeStatus.Pending;
            job.RetryCount += 1;
            job.StartedUtc = null;
            job.LeaseOwner = null;
            job.LeaseExpiresUtc = null;
            job.NextAttemptUtc = now;
            job.FailureCode = "startup_recovery";

            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "JobRecovered",
                EntityId = job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                EntityType = "Job",
                AfterJson = $"{{\"retryCount\":{job.RetryCount}}}",
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
