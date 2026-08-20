using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditRepository"/> against
/// <see cref="CatalogueDbContext"/>. This is a strictly append-only implementation;
/// no update or delete operations are exposed (NFR-PROD-013, CTRL-OGMA-018).
/// </summary>
public sealed class AuditRepository : IAuditRepository, IDisposable
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of <see cref="AuditRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal AuditRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AuditRepository"/>.
    /// </summary>
    public AuditRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // SQLite accepts a single writer. Serializing this append-only stream
            // avoids a thundering herd of busy retries when many authenticated LAN
            // requests finish together, while preserving durable audit ordering.
            using CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, cancellationToken)
                .ConfigureAwait(false);
            CatalogueDbContext context = lease.Context;

            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = auditEvent.EventType,
                EntityId = auditEvent.EntityId,
                ActorId = auditEvent.ActorId,
                AfterJson = auditEvent.Payload,
                Timestamp = auditEvent.TimestampUtc,
                IsLocalOnly = true,
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEvent>> ReadRecentAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        // SQLite EF Core does not support DateTimeOffset in ORDER BY.
        // We load and then sort on the client. For audit reads (max recent N rows)
        // this is acceptable; the index on EventId keeps retrieval fast.
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<AuditEventRow> rows = await context.AuditEvents
            .AsNoTracking()
            .OrderByDescending(e => e.EventId) // EventId is monotonically increasing
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new AuditEvent
        {
            Id = r.EventId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EventType = r.EventType,
            EntityId = r.EntityId,
            ActorId = r.ActorId,
            TimestampUtc = r.Timestamp,
            Payload = r.AfterJson,
        }).ToList();
    }

    /// <inheritdoc />
    public void Dispose() => _writeGate.Dispose();
}
