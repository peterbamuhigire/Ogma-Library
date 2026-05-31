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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        DateTimeOffset retrievedUtc = DateTimeOffset.UtcNow;

        try
        {
            string url = $"api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn13)}&format=json&jscmd=data";
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

        // Open Library wraps the result in a key like "ISBN:9780123456789"
        string key = $"ISBN:{isbn13}";
        if (!root.TryGetProperty(key, out var book))
        {
            // No result
            return null;
        }

        string? title = book.TryGetProperty("title", out var t) ? t.GetString() : null;
        string? description = null;

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
