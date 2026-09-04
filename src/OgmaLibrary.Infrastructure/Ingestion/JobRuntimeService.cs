using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Implements atomic job leases over the existing queue. DateTimeOffset due-date
/// comparisons are deliberately evaluated in memory after a bounded indexed query
/// because SQLite cannot translate them reliably.
/// </summary>
public sealed class JobRuntimeService : IJobRuntimeService
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Creates a runtime backed by an explicit context for tests.</summary>
    public JobRuntimeService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Creates a runtime backed by independent contexts for production.</summary>
    public JobRuntimeService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<JobLease?> ClaimNextAsync(
        IReadOnlyCollection<string> jobTypes,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (jobTypes.Count == 0)
        {
            throw new ArgumentException("At least one job type is required.", nameof(jobTypes));
        }

        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<JobRow> candidates = await context.Jobs
            .Where(job => jobTypes.Contains(job.JobType) &&
                          (job.Status == (int)JobRuntimeStatus.Pending ||
                           job.Status == (int)JobRuntimeStatus.Running))
            .OrderBy(job => job.JobId)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        JobRow? job = candidates.FirstOrDefault(candidate =>
            (candidate.Status == (int)JobRuntimeStatus.Pending ||
             (candidate.LeaseExpiresUtc is not null && candidate.LeaseExpiresUtc < now)) &&
            (candidate.NextAttemptUtc is null || candidate.NextAttemptUtc <= now));
        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        job.Status = (int)JobRuntimeStatus.Running;
        job.RetryCount++;
        job.StartedUtc = now;
        job.LeaseOwner = workerId.Trim();
        job.LeaseExpiresUtc = now.Add(leaseDuration);
        job.NextAttemptUtc = null;
        AddAuditEvent(
            context,
            "JobClaimed",
            job,
            new
            {
                jobType = job.JobType,
                attempt = job.RetryCount,
                leaseSeconds = (int)leaseDuration.TotalSeconds,
            });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new JobLease(
            job.JobId,
            job.JobType,
            job.BookId,
            job.Payload,
            job.RetryCount,
            workerId.Trim(),
            job.LeaseExpiresUtc.Value);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(long jobId, string workerId, CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        JobRow job = await FindJobAsync(lease.Context, jobId, cancellationToken).ConfigureAwait(false);
        EnsureOwner(job, workerId);
        job.Status = (int)JobRuntimeStatus.Completed;
        job.CompletedUtc = DateTimeOffset.UtcNow;
        job.LeaseOwner = null;
        job.LeaseExpiresUtc = null;
        job.FailureCode = null;
        job.ErrorMessage = null;
        AddAuditEvent(
            lease.Context,
            "JobCompleted",
            job,
            new
            {
                jobType = job.JobType,
                attempt = job.RetryCount,
            });
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RenewAsync(
        long jobId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        JobRow job = await FindJobAsync(lease.Context, jobId, cancellationToken).ConfigureAwait(false);
        EnsureOwner(job, workerId);
        job.LeaseExpiresUtc = DateTimeOffset.UtcNow.Add(leaseDuration);
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FailAsync(
        long jobId,
        string workerId,
        JobFailure failure,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Code);
        if (failure.Code.Length > 128 || failure.SafeMessage?.Length > 4096)
        {
            throw new ArgumentException("Failure code or message exceeds the diagnostic contract.", nameof(failure));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        JobRow job = await FindJobAsync(lease.Context, jobId, cancellationToken).ConfigureAwait(false);
        EnsureOwner(job, workerId);
        bool retry = !failure.DeadLetter && failure.Retryable && job.RetryCount < maxAttempts;
        job.Status = (int)(failure.DeadLetter
            ? JobRuntimeStatus.DeadLetter
            : retry
                ? JobRuntimeStatus.Pending
                : JobRuntimeStatus.Failed);
        job.FailureCode = failure.Code.Trim();
        job.ErrorMessage = failure.SafeMessage;
        job.LeaseOwner = null;
        job.LeaseExpiresUtc = null;
        job.NextAttemptUtc = retry ? DateTimeOffset.UtcNow.Add(DefaultRetryDelay) : null;
        job.CompletedUtc = retry ? null : DateTimeOffset.UtcNow;
        AddAuditEvent(
            lease.Context,
            "JobFailed",
            job,
            new
            {
                jobType = job.JobType,
                attempt = job.RetryCount,
                failureCode = job.FailureCode,
                retryScheduled = retry,
                deadLetter = failure.DeadLetter,
            });
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<JobRow> running = await lease.Context.Jobs
            .Where(job => job.Status == (int)JobRuntimeStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<JobRow> expired = running
            .Where(job => job.LeaseExpiresUtc is not null && job.LeaseExpiresUtc < now)
            .ToList();
        foreach (JobRow job in expired)
        {
            job.Status = (int)JobRuntimeStatus.Pending;
            job.LeaseOwner = null;
            job.LeaseExpiresUtc = null;
            job.NextAttemptUtc = now;
            job.FailureCode = "lease_expired";
            job.ErrorMessage = "The previous worker lease expired and the job was returned to the queue.";
            AddAuditEvent(
                lease.Context,
                "JobLeaseRecovered",
                job,
                new
                {
                    jobType = job.JobType,
                    attempt = job.RetryCount,
                });
        }

        if (expired.Count > 0)
        {
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return expired.Count;
    }

    private static void AddAuditEvent(
        CatalogueDbContext context,
        string eventType,
        JobRow job,
        object payload)
    {
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = eventType,
            EntityId = job.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EntityType = "Job",
            AfterJson = JsonSerializer.Serialize(payload),
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
    }

    private static async Task<JobRow> FindJobAsync(
        CatalogueDbContext context,
        long jobId,
        CancellationToken cancellationToken)
    {
        JobRow? job = await context.Jobs.FirstOrDefaultAsync(row => row.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);
        return job ?? throw new KeyNotFoundException($"Job '{jobId}' was not found.");
    }

    private static void EnsureOwner(JobRow job, string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (job.Status != (int)JobRuntimeStatus.Running ||
            !string.Equals(job.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The worker does not own the active job lease.");
        }
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;
        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
