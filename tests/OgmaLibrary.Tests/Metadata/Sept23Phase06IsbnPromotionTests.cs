using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Sept-23 Phase 06 (T06.8, K24; T06.9, K23): the best extracted ISBN reaches the canonical
/// catalogue with provenance, invalid or overridden values never do, and books sharing an
/// ISBN are grouped as one edition while every book gets an edition.
/// </summary>
public sealed class Sept23Phase06IsbnPromotionTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly IsbnPromotionService _service;

    public Sept23Phase06IsbnPromotionTests()
    {
        _service = new IsbnPromotionService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task BestValidEvidence_IsPromotedWithProvenance()
    {
        await AddBookAsync("B1", "9780306406157", artifact: 11);

        Assert.Equal(IsbnPromotionOutcome.Promoted, await _service.PromoteAsync("B1"));

        BookRow book = await _context.Books.AsNoTracking().SingleAsync(b => b.BookId == "B1");
        Assert.Equal("9780306406157", book.IsbnNormalized);
        BookMetadataFieldRow field = await _context.BookMetadataFields.AsNoTracking()
            .SingleAsync(f => f.BookId == "B1" && f.FieldName == "ISBN");
        Assert.Equal("9780306406157", field.Value);
        Assert.Equal("extracted:11", field.Source);
        Assert.Contains(await _context.AuditEvents.AsNoTracking().ToListAsync(), e => e.EventType == "IsbnPromoted");
    }

    [Fact]
    public async Task InvalidChecksum_IsNeverPromoted()
    {
        // 978-9970-02-123-4 from the corpus: a plausible-looking ISBN with a wrong check digit.
        await AddBookAsync("B1", "9789970021234", artifact: 3);

        Assert.Equal(IsbnPromotionOutcome.Invalid, await _service.PromoteAsync("B1"));
        Assert.Null((await _context.Books.AsNoTracking().SingleAsync(b => b.BookId == "B1")).IsbnNormalized);
    }

    [Fact]
    public async Task UserOverride_Wins()
    {
        await AddBookAsync("B1", "9780306406157", artifact: 4);
        _context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = "B1",
            FieldName = "ISBN",
            Value = "9780131103627",
            Source = "user",
            IsOverridden = true,
        });
        BookRow book = await _context.Books.SingleAsync(b => b.BookId == "B1");
        book.IsbnNormalized = "9780131103627";
        await _context.SaveChangesAsync();

        Assert.Equal(IsbnPromotionOutcome.KeptExisting, await _service.PromoteAsync("B1"));
        Assert.Equal("9780131103627", (await _context.Books.AsNoTracking().SingleAsync(b => b.BookId == "B1")).IsbnNormalized);
    }

    [Fact]
    public async Task SameIsbnInTwoForms_GroupsBooksAsOneEdition_AndOthersGetTheirOwn()
    {
        await AddBookAsync("B1", "9780306406157", artifact: 1);
        await AddBookAsync("B2", "0306406152", artifact: 2); // the ISBN-10 of the same edition
        await AddBookAsync("B3", null, artifact: 3);

        await _service.PromoteAsync("B1");
        await _service.PromoteAsync("B2");
        await _service.PromoteAsync("B3");

        List<BookRow> books = await _context.Books.AsNoTracking().OrderBy(b => b.BookId).ToListAsync();
        Assert.All(books, b => Assert.NotNull(b.EditionId));
        Assert.Equal(books[0].EditionId, books[1].EditionId);
        Assert.NotEqual(books[0].EditionId, books[2].EditionId);
        Assert.Equal(3, await _context.Books.CountAsync()); // grouped, never merged
        Assert.Contains(
            await _context.AuditEvents.AsNoTracking().ToListAsync(),
            e => e.EventType == "IdentityEditionAssigned" && e.EntityId == "B2" && e.AfterJson!.Contains("same_isbn", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Promotion_IsIdempotent()
    {
        await AddBookAsync("B1", "9780306406157", artifact: 1);
        await _service.PromoteAsync("B1");
        long? edition = (await _context.Books.AsNoTracking().SingleAsync()).EditionId;

        await _service.PromoteAsync("B1");

        Assert.Equal(edition, (await _context.Books.AsNoTracking().SingleAsync()).EditionId);
        Assert.Equal(1, await _context.BookMetadataFields.CountAsync(f => f.FieldName == "ISBN"));
        Assert.Equal(1, await _context.Editions.CountAsync());
    }

    [Theory]
    [InlineData("0306406152", "9780306406157")]
    [InlineData("9780131103627", "9780131103627")]
    public void Isbn10_ConvertsToIsbn13GroupingKey(string input, string expected) =>
        Assert.Equal(expected, IsbnPromotionService.ToIsbn13(input));

    private async Task AddBookAsync(string bookId, string? isbn, long artifact)
    {
        _context.Books.Add(new BookRow { BookId = bookId, Title = "Title " + bookId, Status = 0 });
        await _context.SaveChangesAsync();
        if (isbn is null)
        {
            return;
        }

        _context.ExtractionArtifacts.Add(new ExtractionArtifactRow
        {
            ExtractionArtifactId = artifact,
            BookId = bookId,
            ContentHash = new string('c', 64),
            ExtractorVersion = "test",
            Status = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();
        _context.ExtractedIsbnEvidence.Add(new ExtractedIsbnEvidenceRow
        {
            BookId = bookId,
            ExtractionArtifactId = artifact,
            IsbnNormalized = isbn,
            IdentifierKind = isbn.Length == 10 ? 0 : 1,
            Source = 2,
            Rank = 0,
            IsBest = true,
            DetectedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }
}
