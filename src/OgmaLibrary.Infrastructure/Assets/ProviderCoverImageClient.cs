using System.Security.Cryptography;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>Validated provider cover bytes ready for local sidecar persistence.</summary>
public sealed record ProviderCoverImage(
    Uri SourceUri,
    byte[] Bytes,
    string Sha256,
    int WidthPx,
    int HeightPx,
    string Format);

/// <summary>
/// Downloads provider cover images through a strict, bounded boundary. This
/// client never follows arbitrary user-supplied hosts and never accepts SVG or
/// HTML masquerading as an image.
/// </summary>
public sealed class ProviderCoverImageClient
{
    private const int MaximumDimension = 4_096;
    private const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "covers.openlibrary.org",
        "books.google.com",
        "books.googleusercontent.com",
    };

    private readonly HttpClient _httpClient;

    /// <summary>Initializes the client with an application-owned HTTP client.</summary>
    public ProviderCoverImageClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>Maximum accepted encoded image size.</summary>
    public static int MaxEncodedBytes => MaximumBytes;

    /// <summary>
    /// Fetches and decodes one provider image after validating its fixed endpoint,
    /// response type, byte ceiling, and decoded dimensions.
    /// </summary>
    public async Task<ProviderCoverImage> DownloadAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        Uri sourceUri = ValidateSourceUri(sourceUrl);
        using HttpRequestMessage request = new(HttpMethod.Get, sourceUri);
        request.Headers.Accept.ParseAdd("image/jpeg");
        request.Headers.Accept.ParseAdd("image/png");
        request.Headers.Accept.ParseAdd("image/webp");

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Provider cover request returned HTTP {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        string format = ValidateContentType(response.Content.Headers.ContentType?.MediaType);
        if (response.Content.Headers.ContentLength > MaximumBytes)
        {
            throw new InvalidDataException("Provider cover exceeds the encoded image limit.");
        }

        using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > MaximumBytes)
            {
                throw new InvalidDataException("Provider cover exceeds the encoded image limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        byte[] bytes = output.ToArray();
        using SKBitmap? bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null || bitmap.Width is <= 0 or > MaximumDimension ||
            bitmap.Height is <= 0 or > MaximumDimension)
        {
            throw new InvalidDataException("Provider cover is not a bounded decodable image.");
        }

        return new ProviderCoverImage(
            sourceUri,
            bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bitmap.Width,
            bitmap.Height,
            format);
    }

    private static Uri ValidateSourceUri(string sourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        if (sourceUrl.Length > 2_048 || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !AllowedHosts.Contains(uri.Host) || uri.IsDefaultPort is false ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Provider cover URL is outside the approved HTTPS endpoints.", nameof(sourceUrl));
        }

        return uri;
    }

    private static string ValidateContentType(string? mediaType) =>
        mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new InvalidDataException("Provider cover content type is not an approved image format."),
        };
}
