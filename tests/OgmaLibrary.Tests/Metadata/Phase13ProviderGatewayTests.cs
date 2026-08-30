using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 13 acceptance tests for normalized durable provider caching.</summary>
public sealed class Phase13ProviderGatewayTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task RepeatedNormalizedLookup_UsesDurableCache()
    {
        var provider = new CountingProvider("FakeProvider");
        var gateway = new MetadataProviderGateway([provider], _context);
        var firstRequest = new MetadataLookupRequest(null, "  The   Title ", " Author ");
        var secondRequest = new MetadataLookupRequest(null, "the title", "author");

        IReadOnlyList<ProviderMetadataResult> first = await gateway.SearchAsync(firstRequest);
        IReadOnlyList<ProviderMetadataResult> second = await gateway.SearchAsync(secondRequest);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, provider.Calls);
        Assert.Single(_context.ProviderCacheEntries);
        Assert.Equal("the title", second[0].Title);
    }

    [Fact]
    public async Task ProviderFailure_IsolatedAndNotPersistedAsDocumentContent()
    {
        var provider = new FailingProvider();
        var gateway = new MetadataProviderGateway([provider], _context);

        IReadOnlyList<ProviderMetadataResult> results = await gateway.SearchAsync(
            new MetadataLookupRequest("9780306406157", null, null));

        Assert.Empty(results);
        Assert.Single(_context.ProviderCacheEntries);
        Assert.True(_context.ProviderCacheEntries.Single().IsNegative);
        Assert.Equal("[]", _context.ProviderCacheEntries.Single().ResponseJson);
    }

    private sealed class CountingProvider : IMetadataProvider
    {
        public CountingProvider(string name) => ProviderName = name;
        public string ProviderName { get; }
        public int Calls { get; private set; }

        public Task<ProviderMetadataResult?> LookupAsync(
            string isbn13,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderMetadataResult?>(null);

        public Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
            MetadataLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ProviderMetadataResult>>([new ProviderMetadataResult(
                ProviderName,
                request.Isbn13 ?? string.Empty,
                request.Title?.Trim().ToLowerInvariant(),
                request.Author is null ? [] : [request.Author],
                null,
                null,
                null,
                null,
                [],
                request.Isbn13,
                0.8,
                DateTimeOffset.UtcNow,
                null)]);
        }
    }

    private sealed class FailingProvider : IMetadataProvider
    {
        public string ProviderName => "FailingProvider";

        public Task<ProviderMetadataResult?> LookupAsync(
            string isbn13,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider offline");

        public Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
            MetadataLookupRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider offline");
    }
}
