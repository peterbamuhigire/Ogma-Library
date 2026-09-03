using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Pdf;

namespace OgmaLibrary.Tests.Search;

/// <summary>Phase 11 acceptance tests for bounded PDF outline extraction.</summary>
public sealed class Phase11TocExtractionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ogma-phase11-toc-{Guid.NewGuid():N}");

    public Phase11TocExtractionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ExtractAsync_RejectsMalformedPdfWithoutEscapingTheBoundary()
    {
        string path = Path.Combine(_root, "malformed.pdf");
        await File.WriteAllTextAsync(path, "%PDF-1.7\nmalformed");

        TocExtractionResult result = await new PdfTableOfContentsService().ExtractAsync(path);

        Assert.Equal(TocExtractionQuality.Failed, result.Quality);
        Assert.Empty(result.Entries);
        Assert.DoesNotContain(path, result.FailureCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_PreservesUnicodeTitlesAndPageTargets()
    {
        string path = Path.Combine(_root, "unicode-outline.pdf");
        using (var document = new PdfDocument())
        {
            PdfPage first = document.AddPage();
            PdfPage second = document.AddPage();
            PdfOutline chapter = document.Outlines.Add("第二章 – Étude", second);
            chapter.Outlines.Add("Résumé", first);
            document.Save(path);
        }

        TocExtractionResult result = await new PdfTableOfContentsService().ExtractAsync(path);

        Assert.Equal(TocExtractionQuality.Complete, result.Quality);
        Assert.Equal(["第二章 – Étude", "Résumé"], result.Entries.Select(entry => entry.Title));
        Assert.Equal([1, 0], result.Entries.Select(entry => entry.PageIndex));
        Assert.Equal([0, 1], result.Entries.Select(entry => entry.Level));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
