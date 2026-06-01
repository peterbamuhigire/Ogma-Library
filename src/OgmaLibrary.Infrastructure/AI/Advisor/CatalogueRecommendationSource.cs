using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Ai.Extensions;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Default Phase 13 recommendation source backed by the local catalogue.</summary>
internal sealed class CatalogueRecommendationSource : IRecommendationSource
{
    private readonly IAdvisorCatalogueReader _catalogueReader;

    public CatalogueRecommendationSource(IAdvisorCatalogueReader catalogueReader)
    {
        ArgumentNullException.ThrowIfNull(catalogueReader);
        _catalogueReader = catalogueReader;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken) =>
        _catalogueReader.GetCandidatesAsync(query, cancellationToken);
}
