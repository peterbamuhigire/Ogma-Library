using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Pdf;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: Phase11RealPdfBenchmark <pdf-directory> [max-files|--pipeline]");
    return 2;
}

string root = Path.GetFullPath(args[0]);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"PDF directory does not exist: {root}");
    return 2;
}

bool runPipeline = args.Length == 2 &&
    args[1].Equals("--pipeline", StringComparison.OrdinalIgnoreCase);
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

if (runPipeline)
{
    return await RunPipelineAsync(files).ConfigureAwait(false);
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

static async Task<int> RunPipelineAsync(IReadOnlyList<string> files)
{
    string dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "ogma-phase11-pipeline-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dataDirectory);

    var fileMap = new Dictionary<string, string>(StringComparer.Ordinal);
    var services = new ServiceCollection();
    services.AddCatalogueContext(dataDirectory, Path.GetDirectoryName(files[0])!);
    services.AddMetadataEnrichment(Path.GetDirectoryName(files[0])!, enableExternalProviders: false);
    services.AddSingleton<IPdfRendererFactory, PdfiumAdapterFactory>();
    services.AddSingleton<IBookFileLocator>(_ => new CorpusFileLocator(fileMap));
    ServiceProvider provider = services.BuildServiceProvider();

    try
    {
        IDbContextFactory<CatalogueDbContext> contextFactory =
            provider.GetRequiredService<IDbContextFactory<CatalogueDbContext>>();
        using (CatalogueDbContext setupContext = contextFactory.CreateDbContext())
        {
            await setupContext.Database.MigrateAsync().ConfigureAwait(false);
            for (int index = 0; index < files.Count; index++)
            {
                string file = files[index];
                string bookId = $"P11REAL{index:000000000000}";
                fileMap[bookId] = file;
                setupContext.Books.Add(new BookRow
                {
                    BookId = bookId,
                    Title = Path.GetFileNameWithoutExtension(file),
                    Sha256Hash = ComputeSha256(file),
                    SizeBytes = new FileInfo(file).Length,
                    Status = 0,
                    IndexStatus = 0,
                });
            }

            await setupContext.SaveChangesAsync().ConfigureAwait(false);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        IExtractionPipelineService pipeline = provider.GetRequiredService<IExtractionPipelineService>();
        ExtractionBatchResult result = await pipeline
            .IndexNextBatchAsync(files.Count, CancellationToken.None)
            .ConfigureAwait(false);
        stopwatch.Stop();

        int extractedPages;
        int extractionArtifacts;
        int searchChunks;
        using (CatalogueDbContext verificationContext = contextFactory.CreateDbContext())
        {
            extractedPages = await verificationContext.ExtractedPages.CountAsync().ConfigureAwait(false);
            extractionArtifacts = await verificationContext.ExtractionArtifacts.CountAsync().ConfigureAwait(false);
            searchChunks = await verificationContext.SearchChunks.CountAsync().ConfigureAwait(false);
        }
        var report = new
        {
            schema = "ogma-phase11-real-pipeline-benchmark-v1",
            fileCount = files.Count,
            booksAttempted = result.BooksAttempted,
            booksIndexed = result.BooksIndexed,
            booksFailed = result.BooksFailed,
            pagesProcessed = result.PagesProcessed,
            failedPages = result.FailedPages,
            extractedPages,
            extractionArtifacts,
            searchChunks,
            elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            files = files.Select(Path.GetFileName).ToArray(),
        };
        Console.WriteLine(JsonSerializer.Serialize(report));
        return result.BooksFailed == 0 && result.FailedPages == 0 ? 0 : 1;
    }
    finally
    {
        provider.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}

static string ComputeSha256(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

internal sealed class CorpusFileLocator(IReadOnlyDictionary<string, string> files) : IBookFileLocator
{
    public Task<string?> LocateAsync(string bookId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(files.TryGetValue(bookId, out string? path) ? path : null);
    }
}

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
