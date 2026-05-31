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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        DateTimeOffset retrievedUtc = DateTimeOffset.UtcNow;

        try
        {
            string url = $"volumes?q=isbn:{Uri.EscapeDataString(isbn13)}";
            using var response = await _httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return BuildFailureResult(isbn13, retrievedUtc, $"HTTP {(int)response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return ParseResponse(isbn13, json, retrievedUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BuildFailureResult(isbn13, retrievedUtc, ex.Message);
        }
    }

    private ProviderMetadataResult? ParseResponse(
        string isbn13,
        string json,
        DateTimeOffset retrievedUtc)
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
            RawJson: json);
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
}
