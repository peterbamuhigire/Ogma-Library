using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Implements durable scan sessions and atomic worker stage leases.</summary>
public sealed class ProcessingStateService : IProcessingStateService
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal ProcessingStateService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public ProcessingStateService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<ScanSessionDescriptor> StartSessionAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        bool enabled = await context.LibraryRoots.AnyAsync(
            root => root.LibraryRootId == rootId.Value &&
                    root.IsEnabled &&
                    root.RootStatus == (int)LibraryRootStatus.Available,
            cancellationToken).ConfigureAwait(false);
        if (!enabled)
        {
            throw new InvalidOperationException("Only an enabled, available library root can be scanned.");
        }

        var row = new ScanSessionRow
        {
            LibraryRootId = rootId.Value,
            Status = (int)ScanSessionStatus.Running,
            StartedUtc = DateTimeOffset.UtcNow,
        };
        context.ScanSessions.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DescribeAsync(context, row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> EnqueueStageAsync(
        long scanSessionId,
        string stageName,
        string subjectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKey);
        if (stageName.Length > 128 || subjectKey.Length > 512)
        {
            throw new ArgumentException("Stage name or subject key exceeds the contract limit.");
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ScanSessionRow session = await FindSessionAsync(context, scanSessionId, cancellationToken).ConfigureAwait(false);
        if ((ScanSessionStatus)session.Status is not ScanSessionStatus.Running)
        {
            throw new InvalidOperationException("Stages can only be added to a running scan session.");
        }

        StageExecutionRow? existing = await context.StageExecutions.FirstOrDefaultAsync(
            row => row.ScanSessionId == scanSessionId &&
                   row.StageName == stageName &&
                   row.SubjectKey == subjectKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.StageExecutionId;
        }

        var stage = new StageExecutionRow
        {
            ScanSessionId = scanSessionId,
            StageName = stageName,
            SubjectKey = subjectKey,
            Status = (int)StageExecutionStatus.Pending,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        context.StageExecutions.Add(stage);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return stage.StageExecutionId;
    }

    /// <inheritdoc />
    public async Task<long> EnqueueStageForRootAsync(
        LibraryRootId rootId,
        long scanSessionId,
        string stageName,
        string subjectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKey);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ScanSessionRow session = await FindSessionAsync(context, scanSessionId, cancellationToken).ConfigureAwait(false);
        if (session.LibraryRootId != rootId.Value ||
            (ScanSessionStatus)session.Status != ScanSessionStatus.Running)
        {
            throw new InvalidOperationException("The supplied session does not belong to the requested root.");
        }

        long[] rootSessionIds = await context.ScanSessions
            .Where(candidate => candidate.LibraryRootId == rootId.Value &&
                                candidate.Status == (int)ScanSessionStatus.Running)
            .Select(candidate => candidate.ScanSessionId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        StageExecutionRow? existing = await context.StageExecutions
            .Where(stage => rootSessionIds.Contains(stage.ScanSessionId) &&
                            stage.StageName == stageName &&
                            stage.SubjectKey == subjectKey &&
                            (stage.Status == (int)StageExecutionStatus.Pending ||
                             stage.Status == (int)StageExecutionStatus.RetryableFailure ||
                             stage.Status == (int)StageExecutionStatus.Running))
            .OrderBy(stage => stage.StageExecutionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.StageExecutionId;
        }

        var stage = new StageExecutionRow
        {
            ScanSessionId = scanSessionId,
            StageName = stageName,
            SubjectKey = subjectKey,
            Status = (int)StageExecutionStatus.Pending,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        context.StageExecutions.Add(stage);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return stage.StageExecutionId;
    }

    /// <inheritdoc />
    public async Task<StageExecutionLease?> ClaimNextAsync(
        string stageName,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<StageExecutionRow> candidates = await context.StageExecutions
            .Where(row => row.StageName == stageName)
            .Where(row => row.Status == (int)StageExecutionStatus.Pending ||
                          row.Status == (int)StageExecutionStatus.RetryableFailure ||
                          row.Status == (int)StageExecutionStatus.Running)
            .OrderBy(row => row.StageExecutionId)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<long> runningSessions = (await context.ScanSessions
            .Where(session => session.Status == (int)ScanSessionStatus.Running)
            .Select(session => session.ScanSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet();
        StageExecutionRow? stage = candidates.FirstOrDefault(row =>
            runningSessions.Contains(row.ScanSessionId) &&
            (row.Status != (int)StageExecutionStatus.Running ||
             (row.LeaseExpiresUtc is not null && row.LeaseExpiresUtc < now)) &&
            (row.NextAttemptUtc is null || row.NextAttemptUtc <= now));
        if (stage is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        stage.Status = (int)StageExecutionStatus.Running;
        stage.Attempt++;
        stage.LeaseOwner = workerId;
        stage.LeaseExpiresUtc = now.Add(leaseDuration);
        stage.NextAttemptUtc = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new StageExecutionLease(
            stage.StageExecutionId,
            stage.ScanSessionId,
            stage.StageName,
            stage.SubjectKey,
            stage.Attempt,
            workerId,
            stage.LeaseExpiresUtc.Value);
    }

    /// <inheritdoc />
    public async Task CompleteStageAsync(
        long stageExecutionId,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        StageExecutionRow stage = await FindStageAsync(context, stageExecutionId, cancellationToken).ConfigureAwait(false);
        EnsureOwner(stage, workerId);
        stage.Status = (int)StageExecutionStatus.Completed;
        stage.LeaseOwner = null;
        stage.LeaseExpiresUtc = null;
        stage.CompletedUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FailStageAsync(
        long stageExecutionId,
        string workerId,
        StageFailure failure,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.Code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        StageExecutionRow stage = await FindStageAsync(context, stageExecutionId, cancellationToken).ConfigureAwait(false);
        EnsureOwner(stage, workerId);
        bool retry = failure.Retryable && stage.Attempt < maxAttempts;
        stage.Status = (int)(retry ? StageExecutionStatus.RetryableFailure : StageExecutionStatus.TerminalFailure);
        stage.ErrorCode = failure.Code;
        stage.ErrorMessage = failure.SafeMessage;
        stage.LeaseOwner = null;
        stage.LeaseExpiresUtc = null;
        stage.NextAttemptUtc = retry ? DateTimeOffset.UtcNow.Add(DefaultRetryDelay) : null;
        stage.CompletedUtc = retry ? null : DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RequestCancellationAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ScanSessionRow session = await FindSessionAsync(context, scanSessionId, cancellationToken).ConfigureAwait(false);
        if ((ScanSessionStatus)session.Status is ScanSessionStatus.Completed or ScanSessionStatus.Failed or ScanSessionStatus.Cancelled)
        {
            return;
        }

        session.Status = (int)ScanSessionStatus.CancellationRequested;
        List<StageExecutionRow> pending = await context.StageExecutions
            .Where(stage => stage.ScanSessionId == scanSessionId &&
                (stage.Status == (int)StageExecutionStatus.Pending ||
                 stage.Status == (int)StageExecutionStatus.RetryableFailure))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (StageExecutionRow stage in pending)
        {
            stage.Status = (int)StageExecutionStatus.Cancelled;
            stage.CompletedUtc = DateTimeOffset.UtcNow;
            stage.NextAttemptUtc = null;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<StageExecutionRow> running = await context.StageExecutions
            .Where(stage => stage.Status == (int)StageExecutionStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<StageExecutionRow> expired = running
            .Where(stage => stage.LeaseExpiresUtc is not null && stage.LeaseExpiresUtc < now)
            .ToList();
        foreach (StageExecutionRow stage in expired)
        {
            stage.Status = (int)StageExecutionStatus.RetryableFailure;
            stage.LeaseOwner = null;
            stage.LeaseExpiresUtc = null;
            stage.ErrorCode = "lease_expired";
            stage.ErrorMessage = "The previous worker lease expired and the stage was returned to the queue.";
            stage.NextAttemptUtc = now;
        }

        if (expired.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return expired.Count;
    }

    /// <inheritdoc />
    public async Task<ScanSessionDescriptor> FinalizeSessionAsync(
        long scanSessionId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        ScanSessionRow session = await FindSessionAsync(context, scanSessionId, cancellationToken).ConfigureAwait(false);
        List<StageExecutionStatus> statuses = await context.StageExecutions
            .Where(stage => stage.ScanSessionId == scanSessionId)
            .Select(stage => (StageExecutionStatus)stage.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if ((ScanSessionStatus)session.Status == ScanSessionStatus.CancellationRequested)
        {
            if (statuses.Any(status => status is StageExecutionStatus.Running))
            {
                throw new InvalidOperationException("A cancelled session cannot be finalized while a stage is running.");
            }

            session.Status = (int)ScanSessionStatus.Cancelled;
        }
        else if (statuses.Any(status => status is StageExecutionStatus.Pending or StageExecutionStatus.Running or StageExecutionStatus.RetryableFailure))
        {
            throw new InvalidOperationException("A scan session still has unfinished stages.");
        }
        else if (statuses.Any(status => status == StageExecutionStatus.TerminalFailure))
        {
            session.Status = (int)ScanSessionStatus.Failed;
        }
        else
        {
            session.Status = (int)ScanSessionStatus.Completed;
        }

        session.CompletedUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DescribeAsync(context, session, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private static async Task<ScanSessionRow> FindSessionAsync(
        CatalogueDbContext context,
        long id,
        CancellationToken cancellationToken)
    {
        ScanSessionRow? session = await context.ScanSessions
            .FirstOrDefaultAsync(row => row.ScanSessionId == id, cancellationToken)
            .ConfigureAwait(false);
        return session ?? throw new KeyNotFoundException($"Scan session '{id}' was not found.");
    }

    private static async Task<StageExecutionRow> FindStageAsync(
        CatalogueDbContext context,
        long id,
        CancellationToken cancellationToken)
    {
        StageExecutionRow? stage = await context.StageExecutions
            .FirstOrDefaultAsync(row => row.StageExecutionId == id, cancellationToken)
            .ConfigureAwait(false);
        return stage ?? throw new KeyNotFoundException($"Stage execution '{id}' was not found.");
    }

    private static void EnsureOwner(StageExecutionRow stage, string workerId)
    {
        if (stage.Status != (int)StageExecutionStatus.Running ||
            !string.Equals(stage.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The worker does not own the active stage lease.");
        }
    }

    private static async Task<ScanSessionDescriptor> DescribeAsync(
        CatalogueDbContext context,
        ScanSessionRow session,
        CancellationToken cancellationToken)
    {
        var counts = await context.StageExecutions
            .Where(stage => stage.ScanSessionId == session.ScanSessionId)
            .GroupBy(stage => stage.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken)
            .ConfigureAwait(false);
        return new ScanSessionDescriptor(
            session.ScanSessionId,
            new LibraryRootId(session.LibraryRootId),
            (ScanSessionStatus)session.Status,
            session.StartedUtc,
            session.CompletedUtc,
            counts.Values.Sum(),
            GetCount(counts, StageExecutionStatus.Completed),
            GetCount(counts, StageExecutionStatus.TerminalFailure));
    }

    private static int GetCount(Dictionary<int, int> counts, StageExecutionStatus status) =>
        counts.TryGetValue((int)status, out int count) ? count : 0;

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
