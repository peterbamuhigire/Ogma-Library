using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>EF Core implementation of <see cref="IAiConsentRepository"/>.</summary>
public sealed class AiConsentRepository : IAiConsentRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    internal AiConsentRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Initializes a new instance of <see cref="AiConsentRepository"/>.</summary>
    public AiConsentRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(AiConsentRecord consent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        AiConsentRecordRow? row = await context.AiConsentRecords
            .FindAsync([consent.Id], cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.AiConsentRecords.Add(ToRow(consent));
        }
        else
        {
            row.Tier = (int)consent.Tier;
            row.Provider = consent.Provider;
            row.Scope = consent.Scope;
            row.GrantedAt = consent.GrantedAt;
            row.RevokedAt = consent.RevokedAt;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AiConsentRecord?> GetActiveConsentAsync(
        AiPrivacyTier tier,
        string provider,
        string scope,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<AiConsentRecordRow> rows = await lease.Context.AiConsentRecords
            .AsNoTracking()
            .Where(c =>
                c.Tier == (int)tier &&
                c.Provider == provider &&
                c.Scope == scope &&
                c.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        AiConsentRecordRow? row = rows.OrderByDescending(c => c.GrantedAt).FirstOrDefault();
        return row is null ? null : ToDomain(row);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllAsync(
        AiPrivacyTier tier,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        int affected = await lease.Context.AiConsentRecords
            .Where(c => c.Tier == (int)tier && c.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.RevokedAt, revokedAt),
                cancellationToken)
            .ConfigureAwait(false);
        return affected;
    }

    private Task<CatalogueContextLease> CreateLeaseAsync(CancellationToken cancellationToken) =>
        CatalogueContextLease.CreateAsync(_contextFactory, _context, cancellationToken);

    private static AiConsentRecordRow ToRow(AiConsentRecord consent) => new()
    {
        Id = consent.Id,
        Tier = (int)consent.Tier,
        Provider = consent.Provider,
        Scope = consent.Scope,
        GrantedAt = consent.GrantedAt,
        RevokedAt = consent.RevokedAt,
    };

    private static AiConsentRecord ToDomain(AiConsentRecordRow row) =>
        new(row.Id, (AiPrivacyTier)row.Tier, row.Provider, row.Scope, row.GrantedAt, row.RevokedAt);
}
