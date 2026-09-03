using System.Text.Json;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
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

    [Fact]
    public async Task ExpiredCache_IsReturnedAsExplicitlyStaleWhenRefreshFails()
    {
        var provider = new FlakyProvider();
        var gateway = new MetadataProviderGateway([provider], _context);
        var request = new MetadataLookupRequest(null, "stale title", null);

        IReadOnlyList<ProviderMetadataResult> fresh = await gateway.SearchAsync(request);
        _context.ProviderCacheEntries.Single().ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        IReadOnlyList<ProviderMetadataResult> stale = await gateway.SearchAsync(request);

        Assert.Single(fresh);
        Assert.Single(stale);
        Assert.True(stale[0].IsStale);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task ExpiredCache_UsesProviderValidatorAndRefreshesWithoutReplacingPayload()
    {
        var provider = new ConditionalProvider();
        _context.ProviderCacheEntries.Add(new ProviderCacheEntryRow
        {
            Provider = provider.ProviderName,
            QueryKey = "isbn:|title:conditional title|author:",
            ResponseJson = JsonSerializer.Serialize(new[]
            {
                new ProviderMetadataResult(
                    provider.ProviderName, string.Empty, "Cached title", [], null, null,
                    null, null, [], null, 0.9, DateTimeOffset.UtcNow.AddDays(-1), null),
            }),
            IsNegative = false,
            RetrievedUtc = DateTimeOffset.UtcNow.AddDays(-31),
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ContractVersion = 1,
            ETag = "\"cache-v1\"",
        });
        await _context.SaveChangesAsync();

        var gateway = new MetadataProviderGateway([provider], _context);
        IReadOnlyList<ProviderMetadataResult> results = await gateway.SearchAsync(
            new MetadataLookupRequest(null, "conditional title", null));

        Assert.Single(results);
        Assert.Equal("Cached title", results[0].Title);
        Assert.False(results[0].IsStale);
        Assert.Equal("\"cache-v1\"", provider.ObservedETag);
        Assert.True(_context.ProviderCacheEntries.Single().ExpiresUtc > DateTimeOffset.UtcNow);
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

    private sealed class FlakyProvider : IMetadataProvider
    {
        public string ProviderName => "FlakyProvider";
        public int Calls { get; private set; }

        public Task<ProviderMetadataResult?> LookupAsync(
            string isbn13,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
            MetadataLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls > 1)
            {
                throw new InvalidOperationException("provider offline");
            }

            return Task.FromResult<IReadOnlyList<ProviderMetadataResult>>([new ProviderMetadataResult(
                ProviderName,
                string.Empty,
                request.Title,
                [],
                null,
                null,
                null,
                null,
                [],
                null,
                0.8,
                DateTimeOffset.UtcNow,
                null)]);
        }
    }

    private sealed class ConditionalProvider : IMetadataProvider
    {
        public string ProviderName => "ConditionalProvider";
        public string? ObservedETag { get; private set; }

        public Task<ProviderMetadataResult?> LookupAsync(
            string isbn13,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderMetadataResult?>(null);

        public Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
            MetadataLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedETag = request.ConditionalETag;
            return Task.FromResult<IReadOnlyList<ProviderMetadataResult>>([new ProviderMetadataResult(
                ProviderName,
                string.Empty,
                null,
                [],
                null,
                null,
                null,
                null,
                [],
                null,
                0,
                DateTimeOffset.UtcNow,
                null,
                ETag: "\"cache-v1\"",
                NotModified: true)]);
        }
    }
}
