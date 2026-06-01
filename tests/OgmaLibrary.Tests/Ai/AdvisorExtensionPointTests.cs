using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Ai.Extensions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 13 internal extension-surface tests.</summary>
public sealed class AdvisorExtensionPointTests
{
    [Fact]
    public async Task ExtensionSources_ResolveThroughDi_AndReadLocalCatalogue()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<ICatalogueReadModel, FakeCatalogueReadModel>()
            .AddSingleton<IMetadataSearchService, FakeMetadataSearchService>()
            .AddAiGatewayCore()
            .BuildServiceProvider();

        var recommendationSource = services.GetRequiredService<IRecommendationSource>();
        var catalogueReader = services.GetRequiredService<IAiCatalogueReader>();

        IReadOnlyList<BookMetadataDto> candidates = await recommendationSource.GetCandidatesAsync(
            new RecommendationQuery("systems"),
            CancellationToken.None);
        BookMetadataDto? byId = await catalogueReader.GetByIdAsync(
            new BookId("BOOK-P13-EXT-001"),
            CancellationToken.None);
        IReadOnlyList<BookMetadataDto> byShelf = await catalogueReader.GetByShelfAsync(
            "shelf-systems",
            CancellationToken.None);

        BookMetadataDto candidate = Assert.Single(candidates);
        Assert.Equal("BOOK-P13-EXT-001", candidate.BookId);
        Assert.NotNull(byId);
        Assert.Equal("Extension Systems", byId!.Title);
        Assert.Single(byShelf);
        Assert.Contains("shelf-systems", byShelf[0].ShelfIds);
    }

    private sealed class FakeMetadataSearchService : IMetadataSearchService
    {
        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string? query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>(
                [new MetadataSearchResult("BOOK-P13-EXT-001", "Extension Systems", "Ogma Team", 100, ["Title"])]);
    }

    private sealed class FakeCatalogueReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            if (filter.ShelfId is null or "shelf-systems")
            {
                yield return new BookSummaryProjection(
                    "BOOK-P13-EXT-001",
                    "Extension Systems",
                    ["Ogma Team"],
                    null,
                    0,
                    null,
                    ["shelf-systems"],
                    null,
                    true,
                    2026);
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default)
        {
            if (bookId != "BOOK-P13-EXT-001")
            {
                return Task.FromResult<BookDetailProjection?>(null);
            }

            return Task.FromResult<BookDetailProjection?>(new BookDetailProjection(
                bookId,
                "Extension Systems",
                ["Ogma Team"],
                2026,
                null,
                null,
                null,
                0,
                null,
                "books/extension-systems.pdf",
                null,
                null,
                null,
                0,
                [new MetadataFieldProjection("Tags", "systems; extensions", "Test", 1.0, false)],
                null));
        }

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new ShelfProjection("shelf-systems", "Systems", false, 1);
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
