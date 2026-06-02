using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>HTTP adapter for the Phase 16 LAN Host API.</summary>
internal sealed class LibraryHostHttpClient : ILibraryHostClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public LibraryHostHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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

        return new LibraryHostHealth(
            request.DisplayName ?? request.Address,
            dto.CertificateFingerprint ?? request.CertificateFingerprint,
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

    public void Dispose() => _httpClient.Dispose();

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
}
