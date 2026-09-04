using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: Phase11RealPdfBenchmark <pdf-directory> [max-files]");
    return 2;
}

string root = Path.GetFullPath(args[0]);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"PDF directory does not exist: {root}");
    return 2;
}

int maxFiles = args.Length == 2 && int.TryParse(args[1], out int parsedMax)
    ? parsedMax
    : int.MaxValue;
if (maxFiles <= 0)
{
    Console.Error.WriteLine("max-files must be greater than zero.");
    return 2;
}

string[] files = Directory.EnumerateFiles(root, "*.pdf", SearchOption.TopDirectoryOnly)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .Take(maxFiles)
    .ToArray();
if (files.Length == 0)
{
    Console.Error.WriteLine("No PDF files were found.");
    return 2;
}

long allocatedBefore = GC.GetTotalAllocatedBytes(true);
Stopwatch total = Stopwatch.StartNew();
var results = new List<FileResult>(files.Length);

foreach (string file in files)
{
    Stopwatch fileTimer = Stopwatch.StartNew();
    int pages = 0;
    int fullPages = 0;
    int partialPages = 0;
    int emptyPages = 0;
    int scannedPages = 0;
    int words = 0;
    string? error = null;

    try
    {
        using var renderer = new PdfiumAdapter(file);
        pages = renderer.PageCount;
        for (int pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            TextLayer layer = renderer.ExtractTextLayer(pageIndex);
            words += layer.Words.Count;
            switch (layer.Quality)
            {
                case ExtractionQuality.Full:
                    fullPages++;
                    break;
                case ExtractionQuality.Partial:
                    partialPages++;
                    break;
                case ExtractionQuality.Scanned:
                    scannedPages++;
                    break;
                default:
                    emptyPages++;
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        error = ex.GetType().Name + ": " + ex.Message;
    }

    fileTimer.Stop();
    results.Add(new FileResult(
        Path.GetFileName(file),
        new FileInfo(file).Length,
        pages,
        fullPages,
        partialPages,
        emptyPages,
        scannedPages,
        words,
        fileTimer.ElapsedMilliseconds,
        error));
}

total.Stop();
long allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
var report = new
{
    schema = "ogma-phase11-real-pdf-benchmark-v1",
    generatedUtc = DateTimeOffset.UtcNow,
    fileCount = results.Count,
    filesWithErrors = results.Count(result => result.Error is not null),
    totalBytes = results.Sum(result => result.Bytes),
    totalPages = results.Sum(result => result.Pages),
    fullPages = results.Sum(result => result.FullPages),
    partialPages = results.Sum(result => result.PartialPages),
    emptyPages = results.Sum(result => result.EmptyPages),
    scannedPages = results.Sum(result => result.ScannedPages),
    totalWords = results.Sum(result => result.Words),
    elapsedMilliseconds = total.ElapsedMilliseconds,
    allocatedBytes,
    files = results,
};

Console.WriteLine(JsonSerializer.Serialize(report));
return report.filesWithErrors == 0 ? 0 : 1;

internal sealed record FileResult(
    string FileName,
    long Bytes,
    int Pages,
    int FullPages,
    int PartialPages,
    int EmptyPages,
    int ScannedPages,
    int Words,
    long ElapsedMilliseconds,
    string? Error);
