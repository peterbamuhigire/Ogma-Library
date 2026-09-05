using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 10 WP2 metadata-search tests.
/// </summary>
public sealed class MetadataSearchServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public MetadataSearchServiceTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task MetadataSearch_ExactTitle_ReturnsBookFirst()
    {
        SeedBook("P10METAEXACT0000000001", "Ogma Search Handbook", "Ada Indexer");
        SeedBook("P10METAPREFIX000000001", "Ogma Search Handbook Companion", "Beta Writer");
        var service = new MetadataSearchService(_context);

        var results = await service.SearchAsync("Ogma Search Handbook", CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Equal("P10METAEXACT0000000001", results[0].BookId);
        Assert.Contains("title:exact", results[0].MatchedFields);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task MetadataSearch_PartialAuthor_ReturnsBook()
    {
        SeedBook("P10METAAUTHOR000000001", "Political Learning", "Jean Jacques Rousseau");
        var service = new MetadataSearchService(_context);

        var results = await service.SearchAsync("rousseau", CancellationToken.None);

        Assert.Contains(results, result =>
            result.BookId == "P10METAAUTHOR000000001" &&
            result.MatchedFields.Contains("author"));
    }

    [Fact]
    public async Task MetadataSearch_StructuredAuthorQuery_RestrictsField()
    {
        SeedBook("P22STRUCTURED000000001", "Political Learning", "Jean Jacques Rousseau");
        SeedBook("P22STRUCTURED000000002", "Rousseau Bibliography", "Another Author");
        var service = new MetadataSearchService(_context);

        IReadOnlyList<MetadataSearchResult> results = await service.SearchAsync(
            "author:rousseau",
            CancellationToken.None);

        Assert.Contains(results, result => result.BookId == "P22STRUCTURED000000001");
        Assert.DoesNotContain(results, result => result.BookId == "P22STRUCTURED000000002");
    }

    [Fact]
    public async Task MetadataSearch_MetadataFieldsAndShelves_ContributeMatches()
    {
        SeedBook(
            "P10METAFIELDS000000001",
            "Classroom Reader",
            "School Librarian",
            tags: "philosophy education",
            description: "A quiet guide to library circles",
            shelfName: "Senior Three");
        var service = new MetadataSearchService(_context);

        var tagResults = await service.SearchAsync("philosophy", CancellationToken.None);
        var shelfResults = await service.SearchAsync("Senior", CancellationToken.None);
        var descriptionResults = await service.SearchAsync("circles", CancellationToken.None);

        Assert.Contains(tagResults, result => result.MatchedFields.Contains("tag"));
        Assert.Contains(shelfResults, result => result.MatchedFields.Contains("shelf"));
        Assert.Contains(descriptionResults, result => result.MatchedFields.Contains("description"));
    }

    [Fact]
    public async Task MetadataSearch_EmptyQuery_ReturnsEmpty()
    {
        SeedBook("P10METAEMPTY0000000001", "Invisible Result", "No One");
        var service = new MetadataSearchService(_context);

        var results = await service.SearchAsync("   ", CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task MetadataSearch_SpecialCharacters_AreHandledLiterally()
    {
        SeedBook("P10METACHARS0000000001", "C# Patterns (2026)", "Dot Net");
        var service = new MetadataSearchService(_context);

        var results = await service.SearchAsync("C# Patterns (2026)", CancellationToken.None);

        Assert.Contains(results, result => result.BookId == "P10METACHARS0000000001");
    }

    [Fact]
    public async Task MetadataSearch_TypoTolerance_FindsTolkien()
    {
        SeedBook("P22FUZZYTOLKIEN0000001", "The Hobbit", "J.R.R. Tolkien");
        var service = new MetadataSearchService(_context);

        var results = await service.SearchAsync("tolkein", CancellationToken.None);

        MetadataSearchResult match = Assert.Single(results);
        Assert.Equal("P22FUZZYTOLKIEN0000001", match.BookId);
        Assert.Contains("author:fuzzy", match.MatchedFields);
        Assert.Equal("J.R.R. Tolkien", match.CorrectionSuggestion);
    }

    [Fact]
    public async Task PerfBenchmark_MetadataSearch_P95_LessThan150ms()
    {
        SeedPerfCorpus(50_000);
        var service = new MetadataSearchService(_context);
        _ = await service.SearchAsync("topic 1", CancellationToken.None);

        var elapsed = new List<long>();
        for (int i = 0; i < 50; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = await service.SearchAsync("topic " + (i % 25).ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
            stopwatch.Stop();
            elapsed.Add(stopwatch.ElapsedMilliseconds);
        }

        elapsed.Sort();
        long p95 = elapsed[(int)Math.Ceiling(elapsed.Count * 0.95) - 1];
        Assert.True(p95 <= 150, $"Metadata search P95 was {p95} ms.");
    }

    private void SeedPerfCorpus(int count)
    {
        var books = new List<BookRow>(count);
        var metadata = new List<BookMetadataFieldRow>(count);
        for (int i = 0; i < count; i++)
        {
            string suffix = i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            string bookId = "P10PERF" + suffix;
            books.Add(new BookRow
            {
                BookId = bookId,
                Title = "Topic " + (i % 25).ToString(System.Globalization.CultureInfo.InvariantCulture) + " Reference " + suffix,
                IsbnNormalized = (9780000000000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
            metadata.Add(new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = "Description",
                Value = "Synthetic metadata search benchmark row " + suffix,
            });
        }

        _context.Books.AddRange(books);
        _context.BookMetadataFields.AddRange(metadata);
        _context.SaveChanges();
    }

    private void SeedBook(
        string bookId,
        string title,
        string author,
        string? tags = null,
        string? description = null,
        string? shelfName = null)
    {
        var book = new BookRow
        {
            BookId = bookId,
            Title = title,
            IsbnNormalized = "978" + Math.Abs(bookId.GetHashCode()).ToString("D10", System.Globalization.CultureInfo.InvariantCulture)[..10],
        };
        var authorRow = new AuthorRow
        {
            NormalizedName = author,
            SortName = author,
        };

        _context.Books.Add(book);
        _context.Authors.Add(authorRow);
        _context.SaveChanges();

        _context.BookAuthors.Add(new BookAuthorRow
        {
            BookId = bookId,
            AuthorId = authorRow.AuthorId,
            Role = "Author",
        });

        if (!string.IsNullOrWhiteSpace(tags))
        {
            _context.BookMetadataFields.Add(new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = "Tags",
                Value = tags,
            });
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            _context.BookMetadataFields.Add(new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = "Description",
                Value = description,
            });
        }

        if (!string.IsNullOrWhiteSpace(shelfName))
        {
            var shelf = new ShelfRow
            {
                ShelfId = "SHELF" + bookId[^10..],
                Name = shelfName,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            _context.Shelves.Add(shelf);
            _context.ShelfBooks.Add(new ShelfBookRow
            {
                ShelfId = shelf.ShelfId,
                BookId = bookId,
                AddedUtc = DateTimeOffset.UtcNow,
            });
        }

        _context.SaveChanges();
    }
}
