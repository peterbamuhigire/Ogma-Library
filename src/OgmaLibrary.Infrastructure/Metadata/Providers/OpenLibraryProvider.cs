using System.Globalization;
using System.Text.Json;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.Infrastructure.Metadata.Providers;

/// <summary>
/// <see cref="IMetadataProvider"/> implementation for the Open Library Books API
/// (FR-META-002). Uses a named <c>HttpClient</c> ("OpenLibrary") injected via
/// <c>IHttpClientFactory</c>.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint: GET https://openlibrary.org/api/books?bibkeys=ISBN:{isbn13}&amp;format=json&amp;jscmd=data
/// </para>
/// <para>
/// Open Library is a free, open service. No API key required.
/// </para>
/// </remarks>
public sealed class OpenLibraryProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="OpenLibraryProvider"/>.
    /// </summary>
    /// <param name="httpClient">The named HTTP client ("OpenLibrary").</param>
    public OpenLibraryProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string ProviderName => "OpenLibrary";

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
            string url = $"api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn13)}&format=json&jscmd=data";
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
        catch (Exception ex)
        {
            return BuildFailureResult(isbn13, retrievedUtc, ex.Message);
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

        if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Author))
        {
            return [];
        }

        DateTimeOffset retrievedUtc = DateTimeOffset.UtcNow;

        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                query.Add($"title={Uri.EscapeDataString(request.Title.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(request.Author))
            {
                query.Add($"author={Uri.EscapeDataString(request.Author.Trim())}");
            }

            query.Add("limit=5");
            query.Add("fields=title,author_name,first_publish_year,publisher,isbn,cover_i,subject,language,number_of_pages_median,ratings_average,ratings_count");

            string url = "search.json?" + string.Join('&', query);
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
        catch (Exception ex)
        {
            return [BuildFailureResult(string.Empty, retrievedUtc, ex.Message)];
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

        // Open Library wraps the result in a key like "ISBN:9780123456789"
        string key = $"ISBN:{isbn13}";
        if (!root.TryGetProperty(key, out var book))
        {
            // No result
            return null;
        }

        string? title = book.TryGetProperty("title", out var t) ? t.GetString() : null;
        string? description = null;
        int? pageCount = book.TryGetProperty("number_of_pages", out var pages)
            && pages.TryGetInt32(out int parsedPages)
                ? parsedPages
                : null;

        if (book.TryGetProperty("description", out var desc))
        {
            description = desc.ValueKind == JsonValueKind.Object
                ? (desc.TryGetProperty("value", out var dv) ? dv.GetString() : null)
                : desc.GetString();
        }

        var authors = new List<string>();
        if (book.TryGetProperty("authors", out var auths) && auths.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in auths.EnumerateArray())
            {
                string? name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    authors.Add(name);
                }
            }
        }

        string? publisher = null;
        if (book.TryGetProperty("publishers", out var pubs) && pubs.ValueKind == JsonValueKind.Array)
        {
            var first = pubs.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                publisher = first.TryGetProperty("name", out var pn) ? pn.GetString() : null;
            }
        }

        int? year = null;
        if (book.TryGetProperty("publish_date", out var pd))
        {
            string? dateStr = pd.GetString();
            if (!string.IsNullOrWhiteSpace(dateStr))
            {
                // Try to extract a 4-digit year anywhere in the string
                foreach (System.Text.RegularExpressions.Match m in
                    System.Text.RegularExpressions.Regex.Matches(dateStr, @"\b(\d{4})\b"))
                {
                    if (int.TryParse(m.Value, out int y) && y >= 1000 && y <= 2100)
                    {
                        year = y;
                        break;
                    }
                }
            }
        }

        string? coverUrl = null;
        if (book.TryGetProperty("cover", out var cover))
        {
            if (cover.TryGetProperty("large", out var large))
            {
                coverUrl = large.GetString();
            }
            else if (cover.TryGetProperty("medium", out var med))
            {
                coverUrl = med.GetString();
            }
        }

        var subjects = new List<string>();
        if (book.TryGetProperty("subjects", out var subs) && subs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in subs.EnumerateArray())
            {
                string? sub = s.TryGetProperty("name", out var sn) ? sn.GetString() : s.GetString();
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    subjects.Add(sub);
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
            Categories: subjects,
            IsbnNormalized: isbn13,
            Confidence: 0.80, // Provider weight per DECISIONS.md D-007
            RetrievedUtc: retrievedUtc,
            RawJson: json,
            PageCount: pageCount,
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

        if (!root.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<ProviderMetadataResult>();
        foreach (var item in docs.EnumerateArray())
        {
            string? title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            var authors = ReadStringArray(item, "author_name");
            var publishers = ReadStringArray(item, "publisher");
            var categories = ReadStringArray(item, "subject");
            var languages = ReadStringArray(item, "language");
            var isbnValues = ReadStringArray(item, "isbn");

            if (string.IsNullOrWhiteSpace(title) && authors.Count == 0 && isbnValues.Count == 0)
            {
                continue;
            }

            int? year = item.TryGetProperty("first_publish_year", out var firstYear)
                && firstYear.TryGetInt32(out int parsedYear)
                    ? parsedYear
                    : null;

            int? pageCount = item.TryGetProperty("number_of_pages_median", out var pages)
                && pages.TryGetInt32(out int parsedPages)
                    ? parsedPages
                    : null;

            double? averageRating = item.TryGetProperty("ratings_average", out var avgRating)
                && avgRating.TryGetDouble(out double parsedRating)
                    ? parsedRating
                    : null;

            int? ratingsCount = item.TryGetProperty("ratings_count", out var ratingCount)
                && ratingCount.TryGetInt32(out int parsedRatingCount)
                    ? parsedRatingCount
                    : null;

            string? coverUrl = null;
            if (item.TryGetProperty("cover_i", out var coverId) &&
                coverId.TryGetInt64(out long parsedCoverId))
            {
                coverUrl = $"https://covers.openlibrary.org/b/id/{parsedCoverId}-L.jpg";
            }

            string? canonicalIsbn = isbnValues
                .FirstOrDefault(i => i.Length == 13) ??
                isbnValues.FirstOrDefault();

            results.Add(new ProviderMetadataResult(
                Provider: ProviderName,
                RequestIsbn: request.Isbn13 ?? string.Empty,
                Title: title,
                Authors: authors,
                Publisher: publishers.FirstOrDefault(),
                Year: year,
                Description: null,
                CoverUrl: coverUrl,
                Categories: categories,
                IsbnNormalized: canonicalIsbn,
                Confidence: 0.80 * ScoreResult(request, title, authors),
                RetrievedUtc: retrievedUtc,
                RawJson: item.GetRawText(),
                AverageRating: averageRating,
                RatingsCount: ratingsCount,
                PageCount: pageCount,
                Language: languages.FirstOrDefault(),
                ETag: etag));
        }

        return results
            .OrderByDescending(r => ScoreResult(request, r.Title, r.Authors))
            .ToList();
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
            score += 0.15 * TokenOverlap(request.Author, string.Join(' ', authors));
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
