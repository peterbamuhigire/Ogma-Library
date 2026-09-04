using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>EF Core implementation of <see cref="IAiQueryHistoryRepository"/>.</summary>
public sealed class AiQueryHistoryRepository : IAiQueryHistoryRepository
{
    private static readonly JsonSerializerOptions ExportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    internal AiQueryHistoryRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Initializes a new instance of <see cref="AiQueryHistoryRepository"/>.</summary>
    public AiQueryHistoryRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(AiQueryHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        lease.Context.AiQueryHistory.Add(new AiQueryHistoryRow
        {
            HistoryId = entry.Id,
            QueryType = entry.QueryType,
            QueryText = entry.QueryText,
            ResponseSummary = entry.ResponseSummary,
            CreatedUtc = entry.OccurredAt,
            IsDeleted = entry.Deleted,
        });
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiQueryHistoryEntry>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<AiQueryHistoryRow> rows = await lease.Context.AiQueryHistory
            .AsNoTracking()
            .Where(row => !row.IsDeleted)
            .OrderByDescending(row => row.QueryId)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<AiQueryHistoryRow> rows = await lease.Context.AiQueryHistory
            .AsNoTracking()
            .Where(row => !row.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<AiQueryHistoryEntry> entries = rows
            .OrderBy(row => row.CreatedUtc)
            .ThenBy(row => row.QueryId)
            .Select(ToDomain);
        await JsonSerializer.SerializeAsync(
                output,
                entries,
                ExportOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        int affected = await lease.Context.AiQueryHistory
            .Where(row => row.HistoryId == id && !row.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.IsDeleted, true),
                cancellationToken)
            .ConfigureAwait(false);
        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<int> HardDeleteAllAsync(CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Context.AiQueryHistory
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<CatalogueContextLease> CreateLeaseAsync(CancellationToken cancellationToken) =>
        CatalogueContextLease.CreateAsync(_contextFactory, _context, cancellationToken);

    private static AiQueryHistoryEntry ToDomain(AiQueryHistoryRow row) =>
        new(row.HistoryId, row.CreatedUtc, row.QueryType, row.QueryText, row.ResponseSummary, row.IsDeleted);
}
