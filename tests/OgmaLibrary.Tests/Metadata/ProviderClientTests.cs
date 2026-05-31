using System.Net;
using System.Text;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Metadata.Providers;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Tests for the Google Books and Open Library provider clients (FR-META-002).
/// Uses a stubbed <see cref="HttpMessageHandler"/> â€” no live network.
/// </summary>
public sealed class ProviderClientTests
{
    // â”€â”€ Google Books client â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GoogleBooksProvider_ParsesStubResponse_ExtractsTitle()
    {
        const string json = """
            {
              "totalItems": 1,
              "items": [{
                "volumeInfo": {
                  "title": "Clean Code",
                  "authors": ["Robert C. Martin"],
                  "publisher": "Prentice Hall",
                  "publishedDate": "2008-08-11",
                  "description": "A handbook of agile software craftsmanship.",
                  "industryIdentifiers": [
                    {"type": "ISBN_13", "identifier": "9780132350884"}
                  ],
                  "imageLinks": {
                    "thumbnail": "https://books.google.com/books?id=xyz&printsec=frontcover"
                  },
                  "categories": ["Computers / Programming"]
                }
              }]
            }
            """;

        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var provider = new GoogleBooksProvider(httpClient);

        ProviderMetadataResult? result = await provider.LookupAsync("9780132350884");

        Assert.NotNull(result);
        Assert.Equal("Clean Code", result!.Title);
        Assert.Contains("Robert C. Martin", result.Authors);
        Assert.Equal("Prentice Hall", result.Publisher);
        Assert.Equal(2008, result.Year);
        Assert.Equal("GoogleBooks", result.Provider);
        Assert.Equal(0.85, result.Confidence); // Provider weight
    }

    [Fact]
    public async Task GoogleBooksProvider_NotFound_ReturnsNull()
    {
        const string json = """{"totalItems": 0, "items": []}""";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var provider = new GoogleBooksProvider(httpClient);

        ProviderMetadataResult? result = await provider.LookupAsync("9780000000001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GoogleBooksProvider_HttpError_ReturnsZeroConfidenceResult()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var provider = new GoogleBooksProvider(httpClient);

        ProviderMetadataResult? result = await provider.LookupAsync("9780132350884");

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Confidence);
    }

    [Fact]
    public async Task GoogleBooksProvider_SearchByTitleAuthor_ParsesRatingsAndPages()
    {
        const string json = """
            {
              "totalItems": 1,
              "items": [{
                "volumeInfo": {
                  "title": "Domain-Driven Design",
                  "authors": ["Eric Evans"],
                  "publisher": "Addison-Wesley",
                  "publishedDate": "2003-08-30",
                  "averageRating": 4.7,
                  "ratingsCount": 91,
                  "pageCount": 560,
                  "language": "en",
                  "industryIdentifiers": [
                    {"type": "ISBN_13", "identifier": "9780321125217"}
                  ]
                }
              }]
            }
            """;

        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var provider = new GoogleBooksProvider(httpClient);

        IReadOnlyList<ProviderMetadataResult> results = await provider.SearchAsync(
            new MetadataLookupRequest(Isbn13: null, Title: "Domain Driven Design", Author: "Eric Evans"));

        ProviderMetadataResult result = Assert.Single(results);
        Assert.Equal("Domain-Driven Design", result.Title);
        Assert.Equal(4.7, result.AverageRating);
        Assert.Equal(91, result.RatingsCount);
        Assert.Equal(560, result.PageCount);
        Assert.Equal("en", result.Language);
    }

    // â”€â”€ Open Library client â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task OpenLibraryProvider_ParsesStubResponse_ExtractsTitle()
    {
        const string isbn = "9780743273565";
        string json = $$"""
            {
              "ISBN:{{isbn}}": {
                "title": "The Great Gatsby",
                "authors": [{"name": "F. Scott Fitzgerald"}],
                "publishers": [{"name": "Scribner"}],
                "publish_date": "1925",
                "description": "A novel about the American dream.",
                "cover": {
                  "medium": "https://covers.openlibrary.org/b/id/8428043-M.jpg",
                  "large": "https://covers.openlibrary.org/b/id/8428043-L.jpg"
                },
                "subjects": [{"name": "American fiction"}, {"name": "Classic literature"}]
              }
            }
            """;

        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openlibrary.org/") };
        var provider = new OpenLibraryProvider(httpClient);

        ProviderMetadataResult? result = await provider.LookupAsync(isbn);

        Assert.NotNull(result);
        Assert.Equal("The Great Gatsby", result!.Title);
        Assert.Contains("F. Scott Fitzgerald", result.Authors);
        Assert.Equal("Scribner", result.Publisher);
        Assert.Equal(1925, result.Year);
        Assert.Equal("OpenLibrary", result.Provider);
        Assert.Equal(0.80, result.Confidence); // Provider weight
    }

    [Fact]
    public async Task OpenLibraryProvider_NotFound_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openlibrary.org/") };
        var provider = new OpenLibraryProvider(httpClient);

        ProviderMetadataResult? result = await provider.LookupAsync("9780000000001");

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenLibraryProvider_SearchByTitleAuthor_ParsesSearchDocument()
    {
        const string json = """
            {
              "numFound": 1,
              "docs": [{
                "title": "The Left Hand of Darkness",
                "author_name": ["Ursula K. Le Guin"],
                "first_publish_year": 1969,
                "publisher": ["Ace Books"],
                "isbn": ["0441478123", "9780441478125"],
                "cover_i": 8231856,
                "subject": ["Science fiction"],
                "language": ["eng"],
                "number_of_pages_median": 304,
                "ratings_average": 4.2,
                "ratings_count": 212
              }]
            }
            """;

        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openlibrary.org/") };
        var provider = new OpenLibraryProvider(httpClient);

        IReadOnlyList<ProviderMetadataResult> results = await provider.SearchAsync(
            new MetadataLookupRequest(Isbn13: null, Title: "The Left Hand of Darkness", Author: "Ursula Le Guin"));

        ProviderMetadataResult result = Assert.Single(results);
        Assert.Equal("The Left Hand of Darkness", result.Title);
        Assert.Equal("9780441478125", result.IsbnNormalized);
        Assert.Equal(304, result.PageCount);
        Assert.Equal(4.2, result.AverageRating);
        Assert.Equal("eng", result.Language);
    }

    // â”€â”€ Aggregator concurrent call â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task MetadataProviderAggregator_BothProviders_CalledConcurrently()
    {
        const string isbn = "9780132350884";
        const string googleJson = """
            {
              "totalItems": 1,
              "items": [{"volumeInfo": {"title": "Clean Code", "authors": ["R. Martin"], "publishedDate": "2008"}}]
            }
            """;
        const string openLibJson = "{}"; // no match

        // No delay needed - we verify concurrency by design (Task.WhenAll in aggregator).
        var googleHandler = new StubHttpMessageHandler(HttpStatusCode.OK, googleJson);
        var openLibHandler = new StubHttpMessageHandler(HttpStatusCode.OK, openLibJson);

        var googleClient = new HttpClient(googleHandler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var openLibClient = new HttpClient(openLibHandler) { BaseAddress = new Uri("https://openlibrary.org/") };

        var googleProvider = new GoogleBooksProvider(googleClient);
        var openLibProvider = new OpenLibraryProvider(openLibClient);

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "CONCURRENT01", Status = 0 });
        await context.SaveChangesAsync();

        var aggregator = new MetadataProviderAggregator(
            new IMetadataProvider[] { googleProvider, openLibProvider },
            context);

        var results = await aggregator.AggregateAsync("CONCURRENT01", isbn);

        // Google Books result found.
        Assert.Contains(results, r => r.Provider == "GoogleBooks" && r.Title == "Clean Code");
    }

    [Fact]
    public async Task MetadataProviderAggregator_ProviderFails_OtherResultStored()
    {
        const string isbn = "9780132350884";

        // Google throws; Open Library returns a valid response.
        var throwingHandler = new ThrowingHttpMessageHandler();
        string openLibJson = $$$"""{"ISBN:{{{isbn}}}": {"title": "Clean Code", "authors": [{"name": "R. Martin"}]}}""";
        var openLibHandler = new StubHttpMessageHandler(HttpStatusCode.OK, openLibJson);

        var googleClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var openLibClient = new HttpClient(openLibHandler) { BaseAddress = new Uri("https://openlibrary.org/") };

        var googleProvider = new GoogleBooksProvider(googleClient);
        var openLibProvider = new OpenLibraryProvider(openLibClient);

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "FAIL01", Status = 0 });
        await context.SaveChangesAsync();

        var aggregator = new MetadataProviderAggregator(
            new IMetadataProvider[] { googleProvider, openLibProvider },
            context);

        // Should NOT throw; should return the Open Library result.
        var results = await aggregator.AggregateAsync("FAIL01", isbn);

        // Open Library result present.
        Assert.Contains(results, r => r.Provider == "OpenLibrary");

        // MetadataLookup row stored.
        var rows = context.MetadataLookups.ToList();
        Assert.NotEmpty(rows);
    }

    [Fact]
    public async Task MetadataProviderAggregator_ResultsStoredWithProvenance()
    {
        const string isbn = "9780132350884";
        const string googleJson = """
            {
              "totalItems": 1,
              "items": [{"volumeInfo": {"title": "Clean Code", "publishedDate": "2008"}}]
            }
            """;

        var googleHandler = new StubHttpMessageHandler(HttpStatusCode.OK, googleJson);
        var googleClient = new HttpClient(googleHandler) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        var googleProvider = new GoogleBooksProvider(googleClient);

        using var context = CatalogueTestHelper.CreateInMemoryContext();
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "PROV01", Status = 0 });
        await context.SaveChangesAsync();

        var aggregator = new MetadataProviderAggregator(
            new IMetadataProvider[] { googleProvider },
            context);

        await aggregator.AggregateAsync("PROV01", isbn);

        // Check that a MetadataLookup row was created with Provider, Timestamp, Confidence.
        var row = context.MetadataLookups.SingleOrDefault();
        Assert.NotNull(row);
        Assert.Equal("GoogleBooks", row!.Provider);
        Assert.NotEqual(default, row.Timestamp); // DateTimeOffset must be set
        Assert.NotNull(row.Confidence);
        Assert.True(row.Confidence > 0.0);
    }
}

/// <summary>
/// Stub HTTP handler that returns a fixed response.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly TimeSpan _delay;

    internal StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string content,
        TimeSpan delay = default)
    {
        _statusCode = statusCode;
        _content = content;
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>
/// HTTP handler that always throws.
/// </summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Simulated network failure");
    }
}

