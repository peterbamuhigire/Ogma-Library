using Microsoft.EntityFrameworkCore;
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
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="JobRecoveryService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public JobRecoveryService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Loads all jobs with <c>Status = Running</c> and resets them to
    /// <c>Status = Pending</c> with <c>RetryCount + 1</c>, appending an audit event
    /// per recovered job (NFR-OGMA-009).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of jobs recovered.</returns>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        List<JobRow> stuck = await _context.Jobs
            .Where(j => j.Status == 1) // Running
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (JobRow job in stuck)
        {
            job.Status = 0; // Pending
            job.RetryCount += 1;
            job.StartedUtc = null;

            _context.AuditEvents.Add(new AuditEventRow
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
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return stuck.Count;
    }
}
