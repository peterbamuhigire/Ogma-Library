using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Host resource cache-aside tests.</summary>
public sealed class CachingLibraryHostClientTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CachingLibraryHostClient_PageRenderMissFetchesAndStores()
    {
        var inner = new RecordingHostClient();
        var cache = new InMemoryOfflineCacheService();
        var client = new CachingLibraryHostClient(inner, cache);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource resource = await client.GetPageRenderAsync(
            request,
            "session-token",
            "book 1",
            pageNumber: 3,
            widthPx: 1200);

        OfflineCacheEntry? cached = await cache.GetAsync($"192.168.1.13:7473:{Fingerprint}", "books/book%201/page/3?widthPx=1200");
        Assert.Equal(1, inner.PageRenderCalls);
        Assert.Equal("image/png", resource.ContentType);
        Assert.Equal([1, 2, 3], cached!.Content);
        Assert.Equal("image/png", cached.ContentType);
    }

    [Fact]
    public async Task CachingLibraryHostClient_PageRenderHitAvoidsNetwork()
    {
        var inner = new RecordingHostClient();
        var cache = new InMemoryOfflineCacheService();
        await cache.PutAsync(new OfflineCacheEntry(
            $"192.168.1.13:7473:{Fingerprint}",
            "books/book%201/page/3?widthPx=1200",
            "\"cached\"",
            [9, 9, 9],
            DateTimeOffset.UtcNow,
            "image/png"));
        var client = new CachingLibraryHostClient(inner, cache);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource resource = await client.GetPageRenderAsync(
            request,
            "session-token",
            "book 1",
            pageNumber: 3,
            widthPx: 1200);

        Assert.Equal(0, inner.PageRenderCalls);
        Assert.Equal("\"cached\"", resource.ETag);
        Assert.Equal([9, 9, 9], resource.Content);
    }

    [Fact]
    public async Task CachingLibraryHostClient_CertificateRotationDoesNotReuseOldHostCache()
    {
        var inner = new RecordingHostClient();
        var cache = new InMemoryOfflineCacheService();
        await cache.PutAsync(new OfflineCacheEntry(
            $"192.168.1.13:7473:{Fingerprint}",
            "books/book%201/page/3?widthPx=1200",
            "\"old-host\"",
            [8, 8, 8],
            DateTimeOffset.UtcNow,
            "image/png"));
        var client = new CachingLibraryHostClient(inner, cache);
        var rotatedRequest = new ClassroomJoinRequest(
            "192.168.1.13",
            7473,
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");

        LibraryHostResource resource = await client.GetPageRenderAsync(
            rotatedRequest,
            "session-token",
            "book 1",
            pageNumber: 3,
            widthPx: 1200);

        Assert.Equal(1, inner.PageRenderCalls);
        Assert.Equal([1, 2, 3], resource.Content);
    }

    [Fact]
    public async Task CachingLibraryHostClient_CachesFileStreamAndAssets()
    {
        var inner = new RecordingHostClient();
        var cache = new InMemoryOfflineCacheService();
        var client = new CachingLibraryHostClient(inner, cache);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        LibraryHostResource pdf = await client.GetFileStreamAsync(request, "session-token", "book-1");
        LibraryHostResource asset = await client.GetAssetAsync(
            request,
            "session-token",
            "/api/v1/assets/cover/hash");

        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal("image/jpeg", asset.ContentType);
        Assert.NotNull(await cache.GetAsync($"192.168.1.13:7473:{Fingerprint}", "books/book-1/file"));
        Assert.NotNull(await cache.GetAsync($"192.168.1.13:7473:{Fingerprint}", "api/v1/assets/cover/hash"));
    }

    [Fact]
    public void LibraryHostClient_IsCachedInClassroomClientServices()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-cached-host-client-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            ILibraryHostClient service = provider.GetRequiredService<ILibraryHostClient>();

            Assert.IsType<CachingLibraryHostClient>(service);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    private sealed class RecordingHostClient : ILibraryHostClient
    {
        public int PageRenderCalls { get; private set; }

        public Task<LibraryHostHealth> GetHealthAsync(
            ClassroomJoinRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSession> IssueSessionAsync(
            ClassroomJoinRequest request,
            Guid profileId,
            ClassroomRole role,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostCatalogueQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostBookDetail> GetBookAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSearchPage> SearchCatalogueAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostSearchQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostAiPayloadPreview> PreviewAiSearchAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostAiSearchResult> SearchAiAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetPageRenderAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            int pageNumber,
            int widthPx,
            CancellationToken cancellationToken = default)
        {
            PageRenderCalls++;
            return Task.FromResult(new LibraryHostResource(
                $"books/{Uri.EscapeDataString(bookId)}/page/{pageNumber}?widthPx={widthPx}",
                "image/png",
                "\"fresh\"",
                [1, 2, 3]));
        }

        public Task<LibraryHostResource> GetFileStreamAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryHostResource(
                $"books/{Uri.EscapeDataString(bookId)}/file",
                "application/pdf",
                null,
                [37, 80, 68, 70]));

        public Task<LibraryHostResource> GetAssetAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string assetUrl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryHostResource(
                assetUrl.TrimStart('/'),
                "image/jpeg",
                "\"asset\"",
                [255, 216]));

        public Task UploadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            EncryptedClassroomSyncBlob blob,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EncryptedClassroomSyncBlob?>(null);
    }
}
