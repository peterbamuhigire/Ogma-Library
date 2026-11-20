using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// The model a semantic generation was built with. It is derived from the vectors stored in
/// that generation (provider key, model name and model version are already persisted on every
/// vector), so no new column or migration is needed (Sept-23 Phase 14, slice 4, D-115).
/// </summary>
internal readonly record struct EmbeddingGenerationIdentity(string ProviderKey, string ModelId, string ModelVersion)
{
    /// <summary>The identity the provider produces now.</summary>
    public static EmbeddingGenerationIdentity Of(IEmbeddingProvider provider) =>
        new(provider.ProviderKey, provider.ModelId, provider.ModelVersion);

    /// <summary>Reads the distinct identities of live vectors in <paramref name="indexVersion"/>.</summary>
    public static async Task<IReadOnlyList<EmbeddingGenerationIdentity>> ReadAsync(
        CatalogueDbContext context,
        string indexVersion,
        CancellationToken cancellationToken)
    {
        var rows = await context.EmbeddingVectors
            .AsNoTracking()
            .Where(vector => vector.IndexVersion == indexVersion && !vector.IsTombstoned)
            .Select(vector => new { vector.ProviderKey, vector.ModelName, vector.ModelVersion })
            .Distinct()
            .Take(16)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(row => new EmbeddingGenerationIdentity(row.ProviderKey, row.ModelName, row.ModelVersion))
            .ToList();
    }
}
