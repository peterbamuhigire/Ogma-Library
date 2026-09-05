using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.Infrastructure.Metadata.Providers;

/// <summary>
/// <see cref="IMetadataProvider"/> implementation for the Google Books Volumes API
/// (FR-META-002). Uses a named <c>HttpClient</c> ("GoogleBooks") injected via
/// <c>IHttpClientFactory</c>.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint: GET https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn13}
/// </para>
/// <para>
/// No API key is required for ISBN lookups at the free tier (100 req/day).
/// A key can be supplied via the "GoogleBooksApiKey" configuration entry.
/// </para>
/// </remarks>
public sealed class GoogleBooksProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="GoogleBooksProvider"/>.
    /// </summary>
    /// <param name="httpClient">The named HTTP client ("GoogleBooks").</param>
    public GoogleBooksProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string ProviderName => "GoogleBooks";

    /// <inheritdoc />
    public async Task<ProviderMetadataResult?> LookupAsync(
        string isbn13,
        CancellationToken cancellationToken = default)
        => await LookupCoreAsync(isbn13, conditionalETag: null, cancellationToken).ConfigureAwait(false);

    private async Task<ProviderMetadataResult?> LookupCoreAsync(
        string isbn13,
        string? conditionalETag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        DateTimeOffset retrievedUtc = DateTimeOffset.UtcNow;

        try
        {
            string url = $"volumes?q=isbn:{Uri.EscapeDataString(isbn13)}";
            using HttpRequestMessage request = CreateRequest(url, conditionalETag);
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return new ProviderMetadataResult(
                    ProviderName, isbn13, null, [], null, null, null, null, [], isbn13, 0,
                    retrievedUtc, null, ETag: response.Headers.ETag?.Tag, NotModified: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return BuildFailureResult(isbn13, retrievedUtc, $"HTTP {(int)response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return ParseResponse(isbn13, json, retrievedUtc, response.Headers.ETag?.Tag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return BuildFailureResult(isbn13, retrievedUtc, "Provider request failed.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Isbn13))
        {
            ProviderMetadataResult? isbnResult = await LookupCoreAsync(
                    request.Isbn13,
                    request.ConditionalETag,
                    cancellationToken)
                .ConfigureAwait(false);
            return isbnResult is null ? [] : [isbnResult];
        }

        string query = BuildSearchQuery(request);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        DateTimeOffset retrievedUtc = DateTimeOffset.UtcNow;

        try
        {
            string url = $"volumes?q={Uri.EscapeDataString(query)}&maxResults=5";
            using HttpRequestMessage requestMessage = CreateRequest(url, request.ConditionalETag);
            using HttpResponseMessage response = await _httpClient
                .SendAsync(requestMessage, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return [new ProviderMetadataResult(
                    ProviderName, string.Empty, null, [], null, null, null, null, [], null, 0,
                    retrievedUtc, null, ETag: response.Headers.ETag?.Tag, NotModified: true)];
            }

            if (!response.IsSuccessStatusCode)
            {
                return [BuildFailureResult(string.Empty, retrievedUtc, $"HTTP {(int)response.StatusCode}")];
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return ParseSearchResponse(request, json, retrievedUtc, response.Headers.ETag?.Tag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [BuildFailureResult(string.Empty, retrievedUtc, "Provider request failed.")];
        }
    }

    private ProviderMetadataResult? ParseResponse(
        string isbn13,
        string json,
        DateTimeOffset retrievedUtc,
        string? etag = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("totalItems", out var totalItems) || totalItems.GetInt32() == 0)
        {
            return null; // No match found
        }

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var item = items.EnumerateArray().FirstOrDefault();
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!item.TryGetProperty("volumeInfo", out var vi))
        {
            return null;
        }

        string? title = vi.TryGetProperty("title", out var t) ? t.GetString() : null;
        string? publisher = vi.TryGetProperty("publisher", out var pub) ? pub.GetString() : null;
        string? description = vi.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        string? language = vi.TryGetProperty("language", out var lang) ? lang.GetString() : null;
        double? averageRating = vi.TryGetProperty("averageRating", out var avgRating)
            && avgRating.TryGetDouble(out double rating)
                ? rating
                : null;
        int? ratingsCount = vi.TryGetProperty("ratingsCount", out var count)
            && count.TryGetInt32(out int parsedCount)
                ? parsedCount
                : null;
        int? pageCount = vi.TryGetProperty("pageCount", out var pages)
            && pages.TryGetInt32(out int parsedPages)
                ? parsedPages
                : null;
        string? coverUrl = null;
        int? year = null;

        if (vi.TryGetProperty("publishedDate", out var pd))
        {
            string? dateStr = pd.GetString();
            if (dateStr?.Length >= 4 && int.TryParse(dateStr[..4], out int y))
            {
                year = y;
            }
        }

        if (vi.TryGetProperty("imageLinks", out var imgs)
            && imgs.TryGetProperty("thumbnail", out var thumb))
        {
            coverUrl = thumb.GetString();
        }

        var authors = new List<string>();
        if (vi.TryGetProperty("authors", out var auths) && auths.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in auths.EnumerateArray())
            {
                string? name = a.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    authors.Add(name);
                }
            }
        }

        var categories = new List<string>();
        if (vi.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cats.EnumerateArray())
            {
                string? cat = c.GetString();
                if (!string.IsNullOrWhiteSpace(cat))
                {
                    categories.Add(cat);
                }
            }
        }

        // Extract canonical ISBN from industry identifiers
        string? canonicalIsbn = null;
        if (vi.TryGetProperty("industryIdentifiers", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in ids.EnumerateArray())
            {
                if (id.TryGetProperty("type", out var idType)
                    && idType.GetString() == "ISBN_13"
                    && id.TryGetProperty("identifier", out var identifier))
                {
                    canonicalIsbn = identifier.GetString();
                    break;
                }
            }
        }

        return new ProviderMetadataResult(
            Provider: ProviderName,
            RequestIsbn: isbn13,
            Title: title,
            Authors: authors,
            Publisher: publisher,
            Year: year,
            Description: description,
            CoverUrl: coverUrl,
            Categories: categories,
            IsbnNormalized: canonicalIsbn ?? isbn13,
            Confidence: 0.85, // Provider weight per DECISIONS.md D-007
            RetrievedUtc: retrievedUtc,
            RawJson: json,
            AverageRating: averageRating,
            RatingsCount: ratingsCount,
            PageCount: pageCount,
            Language: language,
            ETag: etag);
    }

    private List<ProviderMetadataResult> ParseSearchResponse(
        MetadataLookupRequest request,
        string json,
        DateTimeOffset retrievedUtc,
        string? etag = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("totalItems", out var totalItems) || totalItems.GetInt32() == 0)
        {
            return [];
        }

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<ProviderMetadataResult>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("volumeInfo", out var vi))
            {
                continue;
            }

            string itemJson = item.GetRawText();
            ProviderMetadataResult? result = ParseVolumeInfo(
                request,
                vi,
                itemJson,
                retrievedUtc,
                etag);

            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results
            .OrderByDescending(r => ScoreResult(request, r))
            .ToList();
    }

    private ProviderMetadataResult? ParseVolumeInfo(
        MetadataLookupRequest request,
        JsonElement vi,
        string rawJson,
        DateTimeOffset retrievedUtc,
        string? etag = null)
    {
        string? title = vi.TryGetProperty("title", out var t) ? t.GetString() : null;
        string? publisher = vi.TryGetProperty("publisher", out var pub) ? pub.GetString() : null;
        string? description = vi.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        string? language = vi.TryGetProperty("language", out var lang) ? lang.GetString() : null;
        int? year = null;

        if (vi.TryGetProperty("publishedDate", out var pd))
        {
            string? dateStr = pd.GetString();
            if (dateStr?.Length >= 4 && int.TryParse(dateStr[..4], NumberStyles.None, CultureInfo.InvariantCulture, out int y))
            {
                year = y;
            }
        }

        var authors = ReadStringArray(vi, "authors");
        var categories = ReadStringArray(vi, "categories");

        string? coverUrl = null;
        if (vi.TryGetProperty("imageLinks", out var imgs)
            && imgs.TryGetProperty("thumbnail", out var thumb))
        {
            coverUrl = thumb.GetString();
        }

        string? canonicalIsbn = null;
        if (vi.TryGetProperty("industryIdentifiers", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in ids.EnumerateArray())
            {
                if (id.TryGetProperty("type", out var idType)
                    && idType.GetString() == "ISBN_13"
                    && id.TryGetProperty("identifier", out var identifier))
                {
                    canonicalIsbn = identifier.GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(title) && authors.Count == 0 && string.IsNullOrWhiteSpace(canonicalIsbn))
        {
            return null;
        }

        double? averageRating = vi.TryGetProperty("averageRating", out var avgRating)
            && avgRating.TryGetDouble(out double rating)
                ? rating
                : null;
        int? ratingsCount = vi.TryGetProperty("ratingsCount", out var count)
            && count.TryGetInt32(out int parsedCount)
                ? parsedCount
                : null;
        int? pageCount = vi.TryGetProperty("pageCount", out var pages)
            && pages.TryGetInt32(out int parsedPages)
                ? parsedPages
                : null;

        return new ProviderMetadataResult(
            Provider: ProviderName,
            RequestIsbn: request.Isbn13 ?? string.Empty,
            Title: title,
            Authors: authors,
            Publisher: publisher,
            Year: year,
            Description: description,
            CoverUrl: coverUrl,
            Categories: categories,
            IsbnNormalized: canonicalIsbn ?? request.Isbn13,
            Confidence: 0.85 * ScoreResult(request, title, authors),
            RetrievedUtc: retrievedUtc,
            RawJson: rawJson,
            AverageRating: averageRating,
            RatingsCount: ratingsCount,
            PageCount: pageCount,
            Language: language,
            ETag: etag);
    }

    private ProviderMetadataResult BuildFailureResult(
        string isbn13,
        DateTimeOffset retrievedUtc,
        string errorMessage)
    {
        return new ProviderMetadataResult(
            Provider: ProviderName,
            RequestIsbn: isbn13,
            Title: null,
            Authors: [],
            Publisher: null,
            Year: null,
            Description: null,
            CoverUrl: null,
            Categories: [],
            IsbnNormalized: isbn13,
            Confidence: 0.0,
            RetrievedUtc: retrievedUtc,
            RawJson: JsonSerializer.Serialize(new { error = errorMessage }));
    }

    private static HttpRequestMessage CreateRequest(string relativeUrl, string? conditionalETag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(conditionalETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", conditionalETag);
        }

        return request;
    }

    private static string BuildSearchQuery(MetadataLookupRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            parts.Add($"intitle:{request.Title.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Author))
        {
            parts.Add($"inauthor:{request.Author.Trim()}");
        }

        return string.Join(' ', parts);
    }

    private static List<string> ReadStringArray(JsonElement parent, string propertyName)
    {
        var values = new List<string>();
        if (parent.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                string? value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static double ScoreResult(MetadataLookupRequest request, ProviderMetadataResult result) =>
        ScoreResult(request, result.Title, result.Authors);

    private static double ScoreResult(
        MetadataLookupRequest request,
        string? title,
        IReadOnlyList<string> authors)
    {
        double score = 0.55;
        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(title))
        {
            score += 0.30 * TokenOverlap(request.Title, title);
        }

        if (!string.IsNullOrWhiteSpace(request.Author) && authors.Count > 0)
        {
            string authorText = string.Join(' ', authors);
            score += 0.15 * TokenOverlap(request.Author, authorText);
        }

        return Math.Clamp(score, 0.10, 1.0);
    }

    private static double TokenOverlap(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0.0;
        }

        return tokensA.Intersect(tokensB, StringComparer.OrdinalIgnoreCase).Count() /
            (double)Math.Max(tokensA.Count, tokensB.Count);
    }

    private static HashSet<string> Tokenize(string value) =>
        new(
            value.Split([' ', ',', '.', ':', ';', '-', '_', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 1),
            StringComparer.OrdinalIgnoreCase);
}
