using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>EF Core append-only implementation of <see cref="IAiAuditRepository"/>.</summary>
public sealed class AiAuditRepository : IAiAuditRepository
{
    private static readonly JsonSerializerOptions ExportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    internal AiAuditRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Initializes a new instance of <see cref="AiAuditRepository"/>.</summary>
    public AiAuditRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task AppendAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        lease.Context.AiAuditEvents.Add(ToRow(auditEvent));
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiAuditEvent>> GetRecentAsync(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<AiAuditEventRow> rows = await lease.Context.AiAuditEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .Select(ToDomain)
            .ToList();
    }

    /// <inheritdoc />
    public async Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<AiAuditEventRow> rows = await lease.Context.AiAuditEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<AiAuditEvent> auditEvents = rows
            .OrderBy(e => e.OccurredAt)
            .Select(ToDomain);
        await JsonSerializer.SerializeAsync(output, auditEvents, ExportOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<CatalogueContextLease> CreateLeaseAsync(CancellationToken cancellationToken) =>
        CatalogueContextLease.CreateAsync(_contextFactory, _context, cancellationToken);

    private static AiAuditEventRow ToRow(AiAuditEvent auditEvent) => new()
    {
        Id = auditEvent.Id,
        OccurredAt = auditEvent.OccurredAt,
        Tier = (int)auditEvent.Tier,
        Provider = auditEvent.Provider,
        Model = auditEvent.Model,
        PromptTokens = auditEvent.PromptTokens,
        CompletionTokens = auditEvent.CompletionTokens,
        PromptCacheTokens = auditEvent.PromptCacheTokens,
        EstimatedCostUsd = auditEvent.EstimatedCostUsd,
        PayloadHash = auditEvent.PayloadHash,
        ResponseHash = auditEvent.ResponseHash,
        QueryHistoryEntryId = auditEvent.QueryHistoryEntryId,
    };

    private static AiAuditEvent ToDomain(AiAuditEventRow row) =>
        new(
            row.Id,
            row.OccurredAt,
            (AiPrivacyTier)row.Tier,
            row.Provider,
            row.Model,
            row.PayloadHash,
            row.ResponseHash,
            row.PromptTokens,
            row.CompletionTokens,
            row.PromptCacheTokens,
            row.EstimatedCostUsd,
            row.QueryHistoryEntryId);
}
