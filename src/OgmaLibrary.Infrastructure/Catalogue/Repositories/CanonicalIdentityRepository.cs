using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>EF Core read adapter for the canonical identity graph.</summary>
public sealed class CanonicalIdentityRepository : ICanonicalIdentityRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Initializes the runtime repository.</summary>
    /// <param name="contextFactory">Factory for operation-scoped contexts.</param>
    public CanonicalIdentityRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    internal CanonicalIdentityRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CanonicalIdentityProjection?> FindByLegacyBookIdAsync(
        BookId legacyBookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyBookId.Value);
        using CatalogueContextLease lease = await CatalogueContextLease.CreateAsync(
            _contextFactory,
            _context,
            cancellationToken).ConfigureAwait(false);

        string? catalogueItemId = await lease.Context.LegacyIdentityAliases
            .AsNoTracking()
            .Where(alias => alias.LegacyBookId == legacyBookId.Value)
            .Select(alias => alias.CatalogueItemId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return catalogueItemId is null
            ? null
            : await FindProjectionAsync(
                lease.Context,
                catalogueItemId,
                legacyBookId.Value,
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CanonicalIdentityProjection?> FindByCatalogueItemIdAsync(
        CatalogueItemId catalogueItemId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogueItemId.Value);
        using CatalogueContextLease lease = await CatalogueContextLease.CreateAsync(
            _contextFactory,
            _context,
            cancellationToken).ConfigureAwait(false);

        string? legacyBookId = await lease.Context.LegacyIdentityAliases
            .AsNoTracking()
            .Where(alias => alias.CatalogueItemId == catalogueItemId.Value)
            .Select(alias => alias.LegacyBookId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await FindProjectionAsync(
            lease.Context,
            catalogueItemId.Value,
            legacyBookId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BookId?> FindLegacyBookIdAsync(
        CatalogueItemId catalogueItemId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogueItemId.Value);
        using CatalogueContextLease lease = await CatalogueContextLease.CreateAsync(
            _contextFactory,
            _context,
            cancellationToken).ConfigureAwait(false);

        string? legacyBookId = await lease.Context.LegacyIdentityAliases
            .AsNoTracking()
            .Where(alias => alias.CatalogueItemId == catalogueItemId.Value)
            .Select(alias => alias.LegacyBookId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return legacyBookId is null ? null : new BookId(legacyBookId);
    }

    private static async Task<CanonicalIdentityProjection?> FindProjectionAsync(
        CatalogueDbContext context,
        string catalogueItemId,
        string? legacyBookId,
        CancellationToken cancellationToken)
    {
        var identity = await (
            from item in context.CatalogueItems.AsNoTracking()
            join work in context.CanonicalWorks.AsNoTracking() on item.WorkId equals work.WorkId
            join edition in context.CanonicalEditions.AsNoTracking() on item.EditionId equals edition.EditionId
            where item.CatalogueItemId == catalogueItemId && edition.WorkId == item.WorkId
            select new
            {
                item.CatalogueItemId,
                item.WorkId,
                item.EditionId,
                item.PreferredOccurrenceId,
                WorkState = work.ResolutionState,
                EditionState = edition.ResolutionState,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (identity is null)
        {
            return null;
        }

        List<CanonicalFileOccurrenceProjection> occurrences = await (
            from link in context.CatalogueItemOccurrences.AsNoTracking()
            join occurrence in context.FileOccurrences.AsNoTracking()
                on link.FileOccurrenceId equals occurrence.FileOccurrenceId
            where link.CatalogueItemId == catalogueItemId
            orderby occurrence.FileOccurrenceId
            select new CanonicalFileOccurrenceProjection(
                new FileOccurrenceId(occurrence.FileOccurrenceId),
                new LibraryRootId(occurrence.LibraryRootId),
                occurrence.ContentAssetId == null
                    ? null
                    : new ContentAssetId(occurrence.ContentAssetId),
                (AvailabilityStatus)occurrence.AvailabilityStatus))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool requiresSemanticReindex = legacyBookId is not null &&
            await context.Books.AsNoTracking()
                .Where(book => book.BookId == legacyBookId)
                .Select(book => book.EmbeddingStatus != 2)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        return new CanonicalIdentityProjection(
            new CataloguePresentationIdentity(
                new CatalogueItemId(identity.CatalogueItemId),
                new WorkId(identity.WorkId),
                new EditionId(identity.EditionId),
                identity.PreferredOccurrenceId is null
                    ? null
                    : new FileOccurrenceId(identity.PreferredOccurrenceId)),
            (BibliographicResolutionState)identity.WorkState,
            (BibliographicResolutionState)identity.EditionState,
            occurrences,
            requiresSemanticReindex);
    }
}
