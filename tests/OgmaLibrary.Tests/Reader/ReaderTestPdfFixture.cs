using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Creates a deterministic multi-page test PDF using QuestPDF (no external files).
/// The PDF is generated once per test run and reused via the static cache.
/// </summary>
public static class ReaderTestPdfFixture
{
    private static readonly Lazy<string> LazyPdfPath = new(CreateTestPdf, isThreadSafe: true);

    /// <summary>The absolute path to the generated test PDF.</summary>
    public static string PdfPath => LazyPdfPath.Value;

    /// <summary>Number of pages in the test PDF.</summary>
    public const int PageCount = 5;

    /// <summary>Known search term present on page 2 (zero-based page 1).</summary>
    public const string KnownSearchTerm = "SEARCHABLE_OGMA_TEXT";

    private static string CreateTestPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        string dir = Path.Combine(Path.GetTempPath(), "ogma-reader-tests");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test-reader.pdf");

        if (File.Exists(path))
        {
            return path; // Reuse from a previous run in the same process.
        }

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text("Page 1: Introduction to Ogma Library Reader");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text($"Page 2: {KnownSearchTerm} content here");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text("Page 3: Navigation and zoom features");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text("Page 4: Full-screen and display modes");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text("Page 5: Bookmarks and annotations (Phase 09)");
            });
        }).GeneratePdf(path);

        return path;
    }
}
