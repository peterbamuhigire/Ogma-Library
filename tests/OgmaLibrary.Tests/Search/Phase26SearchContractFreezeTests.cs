using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Guards the explicitly versioned Phase 26 search DTO contract.</summary>
public sealed class Phase26SearchContractFreezeTests
{
    [Fact]
    public void SearchContract_V1VersionsAndSemanticResponseShape_AreFrozen()
    {
        Assert.Equal("semantic-search-v1", SearchContractVersions.SemanticResponse);
        Assert.Equal("rrf-v1", SearchContractVersions.CombinedFusion);
        Assert.Equal("hybrid-v1", SearchContractVersions.HybridFusion);
        Assert.Equal("search-retrieval-evaluation-v1", SearchContractVersions.Evaluation);

        string[] responseProperties = typeof(SemanticSearchResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(
            [
                "ProviderUnavailable",
                "UsedExactFallback",
                "Results",
                "Availability",
                "EmbeddingCacheHit",
                "ContractVersion",
            ],
            responseProperties);

        var response = new SemanticSearchResponse(
            ProviderUnavailable: false,
            UsedExactFallback: false,
            Results: []);
        var combined = new CombinedSearchResult("book-1", "Title", "Author", 1, [], []);
        var hybrid = new HybridRankedResult(
            "book-1",
            "Title",
            "Author",
            1,
            1,
            0,
            0,
            0,
            null,
            combined,
            null);

        Assert.Equal(SearchContractVersions.SemanticResponse, response.ContractVersion);
        Assert.Equal(SearchContractVersions.CombinedFusion, combined.FusionVersion);
        Assert.Equal(SearchContractVersions.HybridFusion, hybrid.FusionVersion);
    }
}
