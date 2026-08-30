using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Cache-aside decorator for Host resources needed by offline reading.</summary>
internal sealed class CachingLibraryHostClient : ILibraryHostClient
{
    private readonly ILibraryHostClient _inner;
    private readonly IOfflineCacheService _cache;

    public CachingLibraryHostClient(ILibraryHostClient inner, IOfflineCacheService cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default) =>
        _inner.GetHealthAsync(request, cancellationToken);

    public Task<LibraryHostSession> IssueSessionAsync(
        ClassroomJoinRequest request,
        Guid profileId,
        ClassroomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        _inner.IssueSessionAsync(request, profileId, role, lifetime, cancellationToken);

    public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostCatalogueQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetCataloguePageAsync(request, sessionToken, query, cancellationToken);

    public Task<LibraryHostBookDetail> GetBookAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default) =>
        _inner.GetBookAsync(request, sessionToken, bookId, cancellationToken);

    public Task<LibraryHostSearchPage> SearchCatalogueAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostSearchQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.SearchCatalogueAsync(request, sessionToken, query, cancellationToken);

    public Task<LibraryHostAiPayloadPreview> PreviewAiSearchAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostAiSearchRequest query,
        CancellationToken cancellationToken = default) =>
        _inner.PreviewAiSearchAsync(request, sessionToken, query, cancellationToken);

    public Task<LibraryHostAiSearchResult> SearchAiAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostAiSearchRequest query,
        CancellationToken cancellationToken = default) =>
        _inner.SearchAiAsync(request, sessionToken, query, cancellationToken);

    public Task<LibraryHostResource> GetPageRenderAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        int pageNumber,
        int widthPx,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPx);
        int width = Math.Clamp(widthPx, 320, 2400);
        string resourceKey = $"books/{Uri.EscapeDataString(bookId)}/page/{pageNumber}?widthPx={width}";
        return GetOrFetchAsync(
            request,
            resourceKey,
            () => _inner.GetPageRenderAsync(request, sessionToken, bookId, pageNumber, width, cancellationToken),
            cancellationToken);
    }

    public Task<LibraryHostResource> GetFileStreamAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        string resourceKey = $"books/{Uri.EscapeDataString(bookId)}/file";
        return GetOrFetchAsync(
            request,
            resourceKey,
            () => _inner.GetFileStreamAsync(request, sessionToken, bookId, cancellationToken),
            cancellationToken);
    }

    public Task<LibraryHostResource> GetAssetAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string assetUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetUrl);
        string resourceKey = assetUrl.TrimStart('/');
        return GetOrFetchAsync(
            request,
            resourceKey,
            () => _inner.GetAssetAsync(request, sessionToken, assetUrl, cancellationToken),
            cancellationToken);
    }

    public Task UploadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        EncryptedClassroomSyncBlob blob,
        CancellationToken cancellationToken = default) =>
        _inner.UploadProfileSyncBlobAsync(request, sessionToken, blob, cancellationToken);

    public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        CancellationToken cancellationToken = default) =>
        _inner.DownloadProfileSyncBlobAsync(request, sessionToken, cancellationToken);

    private async Task<LibraryHostResource> GetOrFetchAsync(
        ClassroomJoinRequest request,
        string resourceKey,
        Func<Task<LibraryHostResource>> fetch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string hostKey = CreateCacheScopeKey(request);
        OfflineCacheEntry? cached = await _cache.GetAsync(hostKey, resourceKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return new LibraryHostResource(
                cached.ResourceKey,
                cached.ContentType,
                cached.ETag,
                cached.Content);
        }

        LibraryHostResource resource = await fetch().ConfigureAwait(false);
        await _cache.PutAsync(
                new OfflineCacheEntry(
                    hostKey,
                    resource.ResourceKey,
                    resource.ETag,
                    resource.Content,
                    DateTimeOffset.UtcNow,
                    resource.ContentType),
                cancellationToken)
            .ConfigureAwait(false);
        return resource;
    }

    private static string CreateCacheScopeKey(ClassroomJoinRequest request) =>
        $"{HostTrustService.CreateHostKey(request)}:{request.CertificateFingerprint.Trim().ToLowerInvariant()}";
}
