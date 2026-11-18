using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Builds a real, migrated SQLite catalogue that mirrors what a scan of the Sept-23
/// synthetic corpus (<c>tests/fixtures/corpus/New-SyntheticCorpus.py</c>) stores: no
/// promoted <c>Books.Title</c>, PDF-derived Title/Author metadata fields, promoted valid
/// ISBNs, one duplicate copy attached as a second file of the same book, page text chunked
/// per page with the generator's sentence template, and two image-only scans without text.
/// Sept-23 Phase 13 (task 13.1): the relevance oracle runs over this catalogue.
/// </summary>
internal sealed class SyntheticCorpusCatalogue : IDisposable
{
    /// <summary>Book id of the duplicated work (The Lantern Keeper).</summary>
    public const string LanternKeeperId = "01SYNTHCORPUS0000000000006";

    /// <summary>Book id of the image-only pamphlet whose picture reads "amber library lantern".</summary>
    public const string ScannedPamphletId = "01SYNTHCORPUS0000000000010";

    private static readonly CorpusBook[] Books =
    [
        new("01SYNTHCORPUS0000000000001", "Science/The Physics of Everyday Light.pdf", "The Physics of Everyday Light", "Amara Okello",
            "9780306406157", "optics photons refraction rainbow prism wavelength", 40),
        new("01SYNTHCORPUS0000000000002", "Science/Introduction to Tropical Ecology.pdf", "Introduction to Tropical Ecology", "Joseph Mugisha",
            "9781861978769", "rainforest biodiversity canopy savanna ecosystems Lake Victoria", 60),
        new("01SYNTHCORPUS0000000000003", "Science/Algorithms Explained.pdf", "Algorithms Explained", "Grace Namutebi",
            "9780131103627", "sorting graphs dynamic programming complexity recursion binary search", 120),
        new("01SYNTHCORPUS0000000000004", "History/A Short History of the Great Lakes Kingdoms.pdf", "A Short History of the Great Lakes Kingdoms", "Peter Byaruhanga",
            null, "Buganda Bunyoro Kitara Ankole Toro Chwezi dynasty oral tradition", 80, PrintedIsbn: "978-9970-02-123-4"),
        new("01SYNTHCORPUS0000000000005", "History/Trade Routes of East Africa.pdf", "Trade Routes of East Africa", "Sarah Achieng",
            null, "caravan ivory Zanzibar Mombasa monsoon dhow Swahili coast", 30),
        new(LanternKeeperId, "Fiction/The Lantern Keeper.pdf", "The Lantern Keeper", "Daniel Ssempa",
            "9783161484100", "amber library lantern midnight keeper story chapter", 25, DuplicatePath: "Edge Cases/The Lantern Keeper (copy).pdf"),
        new("01SYNTHCORPUS0000000000007", "Fiction/Ngũgĩ-style Stories — Unicode Title.pdf", "Hadithi za Jioni: Évening Tales", "Wanjirũ Kamau",
            null, "hadithi jioni stories evening village fire", 12),
        new("01SYNTHCORPUS0000000000008", "Science/Big Reference Handbook.pdf", "Big Reference Handbook", "Various",
            "9780198526636", "reference handbook tables constants units appendix", 900),
        new("01SYNTHCORPUS0000000000009", "Edge Cases/untitled_scan_0042.pdf", null, "Unknown",
            null, "mystery unknown anonymous notes", 5, HasMetadata: false),
        new(ScannedPamphletId, "Edge Cases/Scanned Pamphlet (image only).pdf", null, null, null, string.Empty, 1, ImageOnly: true),
        new("01SYNTHCORPUS0000000000011", "Edge Cases/Scanned Handout (golden).pdf", null, null, null, string.Empty, 1, ImageOnly: true),
        new("01SYNTHCORPUS0000000000012",
            "Edge Cases/" + new string('a', 40) + "/" + new string('b', 40) + "/" + new string('c', 40) + "/Deeply Nested.pdf",
            "Deeply Nested", "Nester", null, "nested folder depth", 3),
    ];

    private readonly string _dbPath;

    private SyntheticCorpusCatalogue(CatalogueDbContext context, string dbPath)
    {
        Context = context;
        _dbPath = dbPath;
    }

    /// <summary>The migrated catalogue context holding the corpus.</summary>
    public CatalogueDbContext Context { get; }

    /// <summary>Number of catalogue works (the duplicate is a second file of one work).</summary>
    public static int WorkCount => Books.Length;

    /// <summary>Creates, migrates and seeds a temporary catalogue.</summary>
    public static SyntheticCorpusCatalogue Create()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        context.Database.Migrate();
        var corpus = new SyntheticCorpusCatalogue(context, dbPath);
        corpus.Seed();
        return corpus;
    }

    /// <summary>
    /// Adds OCR text for the scanned pamphlet, as the Phase 17 OCR pipeline would after
    /// recognising its single page.
    /// </summary>
    public void AddPamphletOcrText()
    {
        var page = new ExtractedPageRow
        {
            BookId = ScannedPamphletId,
            PageNumber = 0,
            TextContent = "Scanned image only page: amber library lantern",
            ExtractionQuality = (int)SearchExtractionQuality.Full,
            WordCount = 7,
            Source = "OCR",
            ExtractionMethod = "ocr",
            IsSelectedText = true,
            OcrConfidence = 0.91,
            ExtractionUtc = DateTimeOffset.UtcNow,
        };
        Context.ExtractedPages.Add(page);
        Context.SaveChanges();
        Context.SearchChunks.Add(new SearchChunkRow
        {
            BookId = ScannedPamphletId,
            ExtractedPageId = page.ExtractedPageId,
            ChunkIndex = 0,
            ChunkText = page.TextContent,
            Source = (int)SearchChunkSource.Page,
            TokenCount = 7,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        BookRow book = Context.Books.Single(row => row.BookId == ScannedPamphletId);
        book.TextStatus = (int)BookTextStatus.OcrText;
        book.IsOcrDerived = true;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    private void Seed()
    {
        DateTimeOffset now = new(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        foreach (CorpusBook book in Books)
        {
            Context.Books.Add(new BookRow
            {
                BookId = book.BookId,
                Title = null,
                RelativePath = book.RelativePath,
                IsbnNormalized = book.Isbn,
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                TextStatus = (int)(book.ImageOnly ? BookTextStatus.ImageOnly : BookTextStatus.Searchable),
                Sha256Hash = book.BookId.ToLowerInvariant(),
            });
            Context.BookFiles.Add(new BookFileRow
            {
                BookId = book.BookId,
                RelativePath = book.RelativePath,
                LastSeenUtc = now,
            });
            if (book.DuplicatePath is not null)
            {
                Context.BookFiles.Add(new BookFileRow
                {
                    BookId = book.BookId,
                    RelativePath = book.DuplicatePath,
                    LastSeenUtc = now,
                });
            }

            if (book.HasMetadata && book.Title is not null)
            {
                AddField(book.BookId, "Title", book.Title, now);
                AddField(book.BookId, "Author", book.Author!, now);
                AddField(book.BookId, "Subject", book.Words.Split(' ')[0], now);
            }
        }

        Context.SaveChanges();

        foreach (CorpusBook book in Books)
        {
            SeedPages(book, now);
        }

        Context.ChangeTracker.Clear();
    }

    private void SeedPages(CorpusBook book, DateTimeOffset now)
    {
        var random = new Random(StableSeed(book.RelativePath));
        string[] words = book.Words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pages = new List<ExtractedPageRow>(book.Pages);
        for (int pageIndex = 0; pageIndex < book.Pages; pageIndex++)
        {
            string? text = book.ImageOnly ? null : PageText(book, words, pageIndex, random);
            pages.Add(new ExtractedPageRow
            {
                BookId = book.BookId,
                PageNumber = pageIndex,
                TextContent = text,
                ExtractionQuality = (int)(text is null ? SearchExtractionQuality.Scanned : SearchExtractionQuality.Full),
                WordCount = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0,
                ExtractionUtc = now,
            });
        }

        Context.ExtractedPages.AddRange(pages);
        Context.SaveChanges();
        foreach (ExtractedPageRow page in pages.Where(page => page.TextContent is not null))
        {
            Context.SearchChunks.Add(new SearchChunkRow
            {
                BookId = book.BookId,
                ExtractedPageId = page.ExtractedPageId,
                ChunkIndex = page.PageNumber,
                ChunkText = page.TextContent,
                Source = (int)SearchChunkSource.Page,
                TokenCount = page.WordCount,
                CreatedAtUtc = now,
            });
        }

        Context.SaveChanges();
    }

    private static string PageText(CorpusBook book, string[] words, int pageIndex, Random random)
    {
        if (pageIndex == 0)
        {
            return (book.Title is null ? string.Empty : book.Title + "\n") + "by " + book.Author;
        }

        if (pageIndex == 1)
        {
            string? isbn = book.PrintedIsbn ?? FormatIsbn(book.Isbn);
            return "Copyright page\n" +
                (isbn is null ? string.Empty : "ISBN " + isbn + "\n") +
                "Synthetic test fixture. Original content for Ogma testing.";
        }

        var lines = new List<string>(26);
        if (pageIndex % 10 == 2)
        {
            lines.Add($"Chapter {(pageIndex / 10) + 1}");
        }

        for (int line = 0; line < 25; line++)
        {
            string[] sample = words.OrderBy(_ => random.Next()).Take(3).ToArray();
            lines.Add($"Page {pageIndex + 1} line {line}: the {sample[0]} and the {sample[1]} relate to {sample[2]}.");
        }

        return string.Join('\n', lines);
    }

    private static string? FormatIsbn(string? isbn) =>
        isbn is { Length: 13 } ? $"{isbn[..3]}-{isbn[3]}-{isbn[4..8]}-{isbn[8..12]}-{isbn[12]}" : isbn;

    private static int StableSeed(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
    }

    private void AddField(string bookId, string name, string value, DateTimeOffset now) =>
        Context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = bookId,
            FieldName = name,
            Value = value,
            Source = "PDF",
            Confidence = 0.5,
            SourceTimestamp = now,
        });

    private sealed record CorpusBook(
        string BookId,
        string RelativePath,
        string? Title,
        string? Author,
        string? Isbn,
        string Words,
        int Pages,
        bool HasMetadata = true,
        bool ImageOnly = false,
        string? DuplicatePath = null,
        string? PrintedIsbn = null);
}
