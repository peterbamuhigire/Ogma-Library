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

if (args.Length is < 1 or > 4)
{
    Console.Error.WriteLine("Usage: Phase11RealPdfBenchmark <pdf-directory> [max-files] [--pipeline] [--worker] [--repeat=N]");
    return 2;
}

string root = Path.GetFullPath(args[0]);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"PDF directory does not exist: {root}");
    return 2;
}

bool runPipeline = args.Skip(1).Any(argument =>
    argument.Equals("--pipeline", StringComparison.OrdinalIgnoreCase));
bool runWorker = args.Skip(1).Any(argument =>
    argument.Equals("--worker", StringComparison.OrdinalIgnoreCase));
int maxFiles = args.Skip(1)
    .Where(argument => !argument.StartsWith("--", StringComparison.Ordinal))
    .Select(argument => int.TryParse(argument, out int parsed) ? parsed : -1)
    .DefaultIfEmpty(int.MaxValue)
    .First();
int repeatCount = args.Skip(1)
    .Where(argument => argument.StartsWith("--repeat=", StringComparison.OrdinalIgnoreCase))
    .Select(argument => int.TryParse(argument[9..], out int parsed) ? parsed : -1)
    .DefaultIfEmpty(1)
    .First();
if (maxFiles <= 0)
{
    Console.Error.WriteLine("max-files must be greater than zero.");
    return 2;
}

if (repeatCount <= 0 || repeatCount > 10)
{
    Console.Error.WriteLine("repeat must be between 1 and 10.");
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

if (runWorker)
{
    return await RunWorkerAsync(files, repeatCount).ConfigureAwait(false);
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

static async Task<int> RunWorkerAsync(IReadOnlyList<string> files, int repeatCount)
{
    string sandboxRoot = Path.Combine(
        Path.GetTempPath(),
        "ogma-phase11-worker-" + Guid.NewGuid().ToString("N"));
    string? workerPath = Environment.GetEnvironmentVariable("OGMA_PDF_WORKER_PATH");
    if (string.IsNullOrWhiteSpace(workerPath))
    {
        string repositoryWorkerPath = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "src",
            "OgmaLibrary.Workers",
            "bin",
            "Release",
            "net10.0",
            OperatingSystem.IsWindows() ? "OgmaLibrary.Workers.exe" : "OgmaLibrary.Workers"));
        workerPath = File.Exists(repositoryWorkerPath) ? repositoryWorkerPath : null;
    }

    var client = new PdfWorkerClient(new PdfWorkerOptions
    {
        WorkerPath = workerPath,
        SandboxRoot = sandboxRoot,
        Timeout = TimeSpan.FromSeconds(30),
        MaxMemoryBytes = 768L * 1024L * 1024L,
        CpuTimeLimit = TimeSpan.FromSeconds(15),
    });
    var results = new List<WorkerFileResult>(files.Count * repeatCount);

    try
    {
        foreach (string file in files)
        {
            for (int repeat = 1; repeat <= repeatCount; repeat++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                int pages = 0;
                int words = 0;
                long peakWorkingSetBytes = 0;
                long privateMemoryBytes = 0;
                string? error = null;

                try
                {
                    using PdfWorkerClient.PdfWorkerSession session = client.OpenSession(file);
                    pages = session.PageCount;
                    for (int pageIndex = 0; pageIndex < pages; pageIndex++)
                    {
                        words += session.ExtractTextLayer(pageIndex).Words.Count;
                    }

                    peakWorkingSetBytes = session.PeakWorkingSetBytes;
                    privateMemoryBytes = session.PrivateMemoryBytes;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                }

                stopwatch.Stop();
                results.Add(new WorkerFileResult(
                    Path.GetFileName(file),
                    repeat,
                    pages,
                    words,
                    stopwatch.ElapsedMilliseconds,
                    peakWorkingSetBytes,
                    privateMemoryBytes,
                    error));
            }
        }

        var report = new
        {
            schema = "ogma-phase11-worker-resource-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            fileCount = files.Count,
            repeatCount,
            maxMemoryBytes = 768L * 1024L * 1024L,
            runs = results.Count,
            runsWithErrors = results.Count(result => result.Error is not null),
            maxPeakWorkingSetBytes = results.Max(result => result.PeakWorkingSetBytes),
            maxPrivateMemoryBytes = results.Max(result => result.PrivateMemoryBytes),
            results,
        };
        Console.WriteLine(JsonSerializer.Serialize(report));
        return report.runsWithErrors == 0 &&
               report.maxPrivateMemoryBytes <= report.maxMemoryBytes
            ? 0
            : 1;
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(sandboxRoot))
        {
            Directory.Delete(sandboxRoot, recursive: true);
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

internal sealed record WorkerFileResult(
    string FileName,
    int Repeat,
    int Pages,
    int Words,
    long ElapsedMilliseconds,
    long PeakWorkingSetBytes,
    long PrivateMemoryBytes,
    string? Error);
