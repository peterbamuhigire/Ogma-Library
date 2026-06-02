using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>HTTP adapter for the Phase 16 LAN Host API.</summary>
internal sealed class LibraryHostHttpClient : ILibraryHostClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IHostCertificateFingerprintProbe? _certificateProbe;

    public LibraryHostHttpClient(
        HttpClient httpClient,
        IHostCertificateFingerprintProbe? certificateProbe = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _certificateProbe = certificateProbe;
    }

    public async Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await _httpClient
            .GetAsync(BuildUri(request, "/api/v1/health"), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        HostHealthDto dto = await response.Content
            .ReadFromJsonAsync<HostHealthDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host health response was empty.");
        string? tlsFingerprint = _certificateProbe is null
            ? null
            : await _certificateProbe.GetCertificateFingerprintAsync(request, cancellationToken).ConfigureAwait(false);
        string fingerprint = ResolveHealthFingerprint(request, dto.CertificateFingerprint, tlsFingerprint);

        return new LibraryHostHealth(
            request.DisplayName ?? request.Address,
            fingerprint,
            dto.ContentMode ?? "unknown");
    }

    public async Task<LibraryHostSession> IssueSessionAsync(
        ClassroomJoinRequest request,
        Guid profileId,
        ClassroomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var payload = new SessionIssueRequestDto(
            ClientId: profileId.ToString("N"),
            Role: role.ToString().ToLowerInvariant(),
            LifetimeMinutes: Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes)),
            EnrollmentCode: request.EnrollmentCode);
        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(BuildUri(request, "/api/v1/auth/session"), payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        SessionIssueResponseDto dto = await response.Content
            .ReadFromJsonAsync<SessionIssueResponseDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host session response was empty.");

        return new LibraryHostSession(dto.Token, dto.ExpiresUtc);
    }

    public async Task<LibraryHostCataloguePage> GetCataloguePageAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostCatalogueQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(query);

        using var message = new HttpRequestMessage(HttpMethod.Get, BuildCatalogueUri(request, query));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        CataloguePageDto dto = await response.Content
            .ReadFromJsonAsync<CataloguePageDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host catalogue response was empty.");

        return new LibraryHostCataloguePage(
            dto.Items.Select(Map).ToArray(),
            dto.Page,
            dto.PageSize,
            dto.ReturnedCount,
            dto.HasMore);
    }

    public async Task<LibraryHostBookDetail> GetBookAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        string escapedBookId = Uri.EscapeDataString(bookId);
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(request, $"/api/v1/catalogue/{escapedBookId}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        BookDetailDto dto = await response.Content
            .ReadFromJsonAsync<BookDetailDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host book detail response was empty.");

        return Map(dto);
    }

    public async Task<LibraryHostSearchPage> SearchCatalogueAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(query);
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request, query));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        SearchPageDto dto = await response.Content
            .ReadFromJsonAsync<SearchPageDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host catalogue search response was empty.");

        return new LibraryHostSearchPage(
            dto.Query,
            dto.Items.Select(Map).ToArray(),
            dto.ReturnedCount,
            dto.HasMore);
    }

    public async Task<LibraryHostAiPayloadPreview> PreviewAiSearchAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostAiSearchRequest query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(query);

        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(request, "/api/v1/ai/search/preview"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        message.Content = JsonContent.Create(Map(query with { ConfirmedPayloadPreview = false }));
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureAiSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        AiPayloadPreviewDto dto = await response.Content
            .ReadFromJsonAsync<AiPayloadPreviewDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host AI preview response was empty.");

        return new LibraryHostAiPayloadPreview(
            dto.Tier,
            dto.MetadataFields,
            dto.EstimatedCharacters,
            dto.RequiresConfirmation);
    }

    public async Task<LibraryHostAiSearchResult> SearchAiAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostAiSearchRequest query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(query);

        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(request, "/api/v1/ai/search"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        message.Content = JsonContent.Create(Map(query));
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureAiSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        AiSearchResultDto dto = await response.Content
            .ReadFromJsonAsync<AiSearchResultDto>(cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Host AI search response was empty.");

        return new LibraryHostAiSearchResult(
            dto.Answer,
            dto.Citations.Select(Map).ToArray(),
            dto.TokensUsed,
            dto.EstimatedCostUsd,
            dto.WasProviderCalled);
    }

    public Task<LibraryHostResource> GetPageRenderAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        int pageNumber,
        int widthPx,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPx);
        string escapedBookId = Uri.EscapeDataString(bookId);
        int width = Math.Clamp(widthPx, 320, 2400);
        string resourceKey = $"books/{escapedBookId}/page/{pageNumber}?widthPx={width}";
        return GetResourceAsync(
            sessionToken,
            BuildUri(request, $"/api/v1/{resourceKey}"),
            resourceKey,
            cancellationToken);
    }

    public Task<LibraryHostResource> GetFileStreamAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        string escapedBookId = Uri.EscapeDataString(bookId);
        string resourceKey = $"books/{escapedBookId}/file";
        return GetResourceAsync(
            sessionToken,
            BuildUri(request, $"/api/v1/{resourceKey}"),
            resourceKey,
            cancellationToken);
    }

    public Task<LibraryHostResource> GetAssetAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string assetUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetUrl);
        if (!Uri.TryCreate(assetUrl, UriKind.Relative, out Uri? relativeUri) ||
            !assetUrl.StartsWith("/api/v1/assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Asset URL must be a Host asset URL.", nameof(assetUrl));
        }

        string resourceKey = assetUrl.TrimStart('/');
        return GetResourceAsync(
            sessionToken,
            BuildUri(request, assetUrl),
            resourceKey,
            cancellationToken);
    }

    public async Task UploadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        EncryptedClassroomSyncBlob blob,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blob.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(blob.ContentType);

        using var message = new HttpRequestMessage(HttpMethod.Put, BuildUri(request, "/api/v1/profile/sync"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        message.Headers.TryAddWithoutValidation("X-Ogma-Sync-Version", blob.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        message.Content = new ByteArrayContent(blob.Content);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue(blob.ContentType);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(request, "/api/v1/profile/sync"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        int version = 1;
        if (response.Headers.TryGetValues("X-Ogma-Sync-Version", out IEnumerable<string>? versions) &&
            int.TryParse(versions.FirstOrDefault(), out int parsedVersion) &&
            parsedVersion > 0)
        {
            version = parsedVersion;
        }

        return new EncryptedClassroomSyncBlob(version, contentType, content);
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<LibraryHostResource> GetResourceAsync(
        string sessionToken,
        Uri uri,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        string? eTag = response.Headers.ETag?.Tag;
        return new LibraryHostResource(resourceKey, contentType, eTag, content);
    }

    private static Uri BuildCatalogueUri(ClassroomJoinRequest request, LibraryHostCatalogueQuery query)
    {
        var parts = new List<string>
        {
            $"page={Math.Max(1, query.Page)}",
            $"pageSize={Math.Clamp(query.PageSize, 1, 100)}",
        };
        AddQueryPart(parts, "title", query.Title);
        AddQueryPart(parts, "author", query.Author);
        AddQueryPart(parts, "shelfId", query.ShelfId);
        if (query.Status is not null)
        {
            parts.Add($"status={query.Status.Value}");
        }

        return BuildUri(request, $"/api/v1/catalogue?{string.Join('&', parts)}");
    }

    private static Uri BuildSearchUri(ClassroomJoinRequest request, LibraryHostSearchQuery query)
    {
        var parts = new List<string>
        {
            $"pageSize={Math.Clamp(query.PageSize, 1, 50)}",
        };
        AddQueryPart(parts, "q", query.Query);
        return BuildUri(request, $"/api/v1/catalogue/search?{string.Join('&', parts)}");
    }

    private static void AddQueryPart(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static Uri BuildUri(ClassroomJoinRequest request, string pathAndQuery)
    {
        var builder = new UriBuilder(Uri.UriSchemeHttps, request.Address, request.Port)
        {
            Path = pathAndQuery.Split('?', 2)[0],
        };
        int queryIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            builder.Query = pathAndQuery[(queryIndex + 1)..];
        }

        return builder.Uri;
    }

    private static string ResolveHealthFingerprint(
        ClassroomJoinRequest request,
        string? healthFingerprint,
        string? tlsFingerprint)
    {
        if (!string.IsNullOrWhiteSpace(healthFingerprint) &&
            !string.IsNullOrWhiteSpace(tlsFingerprint) &&
            !healthFingerprint.Equals(tlsFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Host health fingerprint does not match the TLS certificate fingerprint.");
        }

        if (!string.IsNullOrWhiteSpace(tlsFingerprint))
        {
            return tlsFingerprint;
        }

        return healthFingerprint ?? request.CertificateFingerprint;
    }

    private static LibraryHostBookSummary Map(CatalogueBookDto dto) =>
        new(
            dto.BookId,
            dto.Title,
            dto.Authors,
            dto.Status,
            dto.Rating,
            dto.ShelfIds,
            dto.ReadingProgressPct,
            dto.IsAvailable,
            dto.Year,
            dto.ContentHash,
            new LibraryHostAssetLinks(
                dto.Assets.CoverUrl,
                dto.Assets.SpineUrl,
                dto.Assets.ThumbnailUrl));

    private static LibraryHostBookDetail Map(BookDetailDto dto) =>
        new(
            dto.BookId,
            dto.Title,
            dto.Authors,
            dto.Year,
            dto.Isbn,
            dto.Doi,
            dto.Rating,
            dto.Status,
            dto.ContentHash,
            dto.SizeBytes,
            dto.ReadingProgress is null ? null : Map(dto.ReadingProgress),
            dto.Annotations,
            dto.MetadataFields.Select(Map).ToArray(),
            dto.ReadingMemory is null ? null : Map(dto.ReadingMemory),
            dto.IsOcrDerived,
            dto.IsPasswordProtected,
            new LibraryHostAssetLinks(
                dto.Assets.CoverUrl,
                dto.Assets.SpineUrl,
                dto.Assets.ThumbnailUrl));

    private static LibraryHostReadingProgress Map(ReadingProgressDto dto) =>
        new(dto.BookId, dto.CurrentPage, dto.CompletionPct, dto.LastReadUtc, dto.Status);

    private static LibraryHostMetadataField Map(MetadataFieldDto dto) =>
        new(dto.FieldName, dto.Value, dto.Source, dto.Confidence, dto.IsOverridden);

    private static LibraryHostReadingMemorySummary Map(ReadingMemoryDto dto) =>
        new(dto.Disposition, dto.KeyInsight, dto.UpdatedAtUtc);

    private static LibraryHostSearchResult Map(SearchResultDto dto) =>
        new(dto.BookId, dto.Title, dto.Author, dto.Score, dto.MatchedFields);

    private static AiSearchRequestDto Map(LibraryHostAiSearchRequest query) =>
        new(
            query.ProfileId,
            query.Query,
            query.LibraryId,
            query.RequestedTier,
            query.ConfirmedPayloadPreview);

    private static LibraryHostAiCitation Map(AiCitationDto dto) =>
        new(dto.BookId, dto.Title, dto.PageNumber);

    private static async Task EnsureAiSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        LanHostErrorDto? error = null;
        try
        {
            error = await response.Content
                .ReadFromJsonAsync<LanHostErrorDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
        }

        string message = error is null
            ? $"Host AI search failed with HTTP {(int)response.StatusCode}."
            : $"{error.Code}: {error.Message}";
        throw new InvalidOperationException(message);
    }

    private sealed record HostHealthDto(
        string? State,
        int Port,
        string? BindAddress,
        string? CertificateFingerprint,
        bool RequiresAuth,
        string? ContentMode);

    private sealed record SessionIssueRequestDto(
        string ClientId,
        string Role,
        int LifetimeMinutes,
        string? EnrollmentCode);

    private sealed record SessionIssueResponseDto(
        string Token,
        DateTimeOffset ExpiresUtc);

    private sealed record CataloguePageDto(
        IReadOnlyList<CatalogueBookDto> Items,
        int Page,
        int PageSize,
        int ReturnedCount,
        bool HasMore);

    private sealed record CatalogueBookDto(
        string BookId,
        string? Title,
        IReadOnlyList<string> Authors,
        int Status,
        int? Rating,
        IReadOnlyList<string> ShelfIds,
        double? ReadingProgressPct,
        bool IsAvailable,
        int? Year,
        string? ContentHash,
        AssetLinksDto Assets);

    private sealed record AssetLinksDto(
        string? CoverUrl,
        string? SpineUrl,
        string? ThumbnailUrl);

    private sealed record BookDetailDto(
        string BookId,
        string? Title,
        IReadOnlyList<string> Authors,
        int? Year,
        string? Isbn,
        string? Doi,
        int? Rating,
        int Status,
        string? ContentHash,
        long? SizeBytes,
        ReadingProgressDto? ReadingProgress,
        int Annotations,
        IReadOnlyList<MetadataFieldDto> MetadataFields,
        ReadingMemoryDto? ReadingMemory,
        bool IsOcrDerived,
        bool IsPasswordProtected,
        AssetLinksDto Assets);

    private sealed record ReadingProgressDto(
        string BookId,
        int CurrentPage,
        double CompletionPct,
        DateTimeOffset? LastReadUtc,
        int Status);

    private sealed record MetadataFieldDto(
        string FieldName,
        string? Value,
        string? Source,
        double? Confidence,
        bool IsOverridden);

    private sealed record ReadingMemoryDto(
        int? Disposition,
        string? KeyInsight,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record SearchPageDto(
        string Query,
        IReadOnlyList<SearchResultDto> Items,
        int ReturnedCount,
        bool HasMore);

    private sealed record SearchResultDto(
        string BookId,
        string? Title,
        string? Author,
        int Score,
        IReadOnlyList<string> MatchedFields);

    private sealed record AiSearchRequestDto(
        Guid ProfileId,
        string Query,
        string LibraryId,
        OgmaLibrary.Domain.Ai.AiPrivacyTier RequestedTier,
        bool ConfirmedPayloadPreview);

    private sealed record AiPayloadPreviewDto(
        OgmaLibrary.Domain.Ai.AiPrivacyTier Tier,
        IReadOnlyDictionary<string, string> MetadataFields,
        int EstimatedCharacters,
        bool RequiresConfirmation);

    private sealed record AiSearchResultDto(
        string Answer,
        IReadOnlyList<AiCitationDto> Citations,
        int TokensUsed,
        decimal EstimatedCostUsd,
        bool WasProviderCalled);

    private sealed record AiCitationDto(
        string BookId,
        string? Title,
        int? PageNumber);

    private sealed record LanHostErrorDto(
        string Code,
        string Message);
}
