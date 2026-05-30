// Spike S02: PDF fixture generator using QuestPDF (Community licence)
// Generates three synthetic PDFs into ../fixtures/

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

string fixturesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
Directory.CreateDirectory(fixturesDir);

Console.WriteLine($"Writing fixtures to: {fixturesDir}");

// ------------------------------------------------------------------
// Fixture 1: gc-simple-text.pdf  (~5 text pages)
// ------------------------------------------------------------------
string simpleTextPath = Path.Combine(fixturesDir, "gc-simple-text.pdf");
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.Content().Column(col =>
        {
            for (int p = 1; p <= 5; p++)
            {
                if (p > 1) col.Item().PageBreak();
                col.Item().Text($"Page {p} — Simple Text Fixture").FontSize(18).Bold();
                col.Item().Text(GenerateParagraphs(6, p)).FontSize(11);
            }
        });
    });
}).GeneratePdf(simpleTextPath);
Console.WriteLine($"  Written: {simpleTextPath}");

// ------------------------------------------------------------------
// Fixture 2: gc-large.pdf  (~200 pages — stand-in for 1000pp, downscaled)
// ------------------------------------------------------------------
string largePath = Path.Combine(fixturesDir, "gc-large.pdf");

var largeDoc = Document.Create(container =>
{
    for (int p = 1; p <= 200; p++)
    {
        int pageNum = p;
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.Content().Column(col =>
            {
                col.Item().Text($"Page {pageNum} — Large Document Fixture").FontSize(18).Bold();
                col.Item().Text(GenerateParagraphs(8, pageNum)).FontSize(10);
            });
        });
    }
});
largeDoc.GeneratePdf(largePath);
Console.WriteLine($"  Written: {largePath}");

// ------------------------------------------------------------------
// Fixture 3: gc-two-column.pdf  (2-column layout, 10 pages)
// ------------------------------------------------------------------
string twoColumnPath = Path.Combine(fixturesDir, "gc-two-column.pdf");

var twoColDoc = Document.Create(container =>
{
    for (int p = 1; p <= 10; p++)
    {
        int pageNum = p;
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.Content().Column(col =>
            {
                col.Item().Text($"Page {pageNum} — Two-Column Layout Fixture").FontSize(14).Bold();
                col.Item().Height(10);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(GenerateParagraphs(5, pageNum * 10)).FontSize(9);
                    row.ConstantItem(20);
                    row.RelativeItem().Text(GenerateParagraphs(5, pageNum * 10 + 5)).FontSize(9);
                });
            });
        });
    }
});
twoColDoc.GeneratePdf(twoColumnPath);
Console.WriteLine($"  Written: {twoColumnPath}");

Console.WriteLine("Fixture generation complete.");

static string GenerateParagraphs(int count, int seed)
{
    string[] words = [
        "library", "book", "reader", "chapter", "knowledge", "spine",
        "catalogue", "shelf", "author", "title", "page", "text", "index",
        "search", "document", "collection", "digital", "archive", "render",
        "content", "metadata", "font", "layout", "column", "paragraph",
        "sentence", "word", "character", "glyph", "typeface", "margin",
        "footnote", "header", "footer", "section", "volume", "edition",
        "publisher", "reader", "bookmark", "annotation", "highlight"
    ];

    var sb = new System.Text.StringBuilder();
    var rng = new Random(seed * 7919 + count);
    for (int p = 0; p < count; p++)
    {
        int sentenceCount = 5 + rng.Next(5);
        for (int s = 0; s < sentenceCount; s++)
        {
            int wordCount = 8 + rng.Next(10);
            string firstWord = words[rng.Next(words.Length)];
            sb.Append(char.ToUpper(firstWord[0]));
            sb.Append(firstWord[1..]);
            for (int w = 1; w < wordCount; w++)
            {
                sb.Append(' ');
                sb.Append(words[rng.Next(words.Length)]);
            }
            sb.Append(". ");
        }
        sb.AppendLine();
        sb.AppendLine();
    }
    return sb.ToString();
}
