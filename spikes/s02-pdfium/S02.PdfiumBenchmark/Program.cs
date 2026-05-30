// =============================================================================
// Spike S02 — PDFium Wrapper Benchmark
// Candidate A: PDFtoImage 4.1.0 (sungaila, SkiaSharp-based)
// Candidate B: Docnet.Core 2.6.0 (GowenGit, lower-level PDFium)
// =============================================================================

await BenchmarkRunner.RunAsync(args);

// =============================================================================
// IPdfRenderer interface (consistent with HLD §F)
// =============================================================================

/// <summary>Adapter interface consistent with HLD §F: render a page to PNG bytes.</summary>
interface IPdfRenderer : IDisposable
{
    string Name { get; }
    int GetPageCount(string path);
    Task<byte[]> RenderPageAsync(string path, int pageIndex, float scale, CancellationToken ct);
}

// =============================================================================
// Adapter A: PDFtoImage (sungaila)
// =============================================================================

class PdfToImageRenderer : IPdfRenderer
{
    public string Name => "PDFtoImage 4.1.0 (sungaila/SkiaSharp)";

    public int GetPageCount(string path)
    {
        using var stream = File.OpenRead(path);
        return PDFtoImage.Conversion.GetPageCount(stream);
    }

    public Task<byte[]> RenderPageAsync(string path, int pageIndex, float scale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(path);
        // PDFtoImage v4 API: ToImage(Stream, Index, RenderOptions?)
        using SkiaSharp.SKBitmap bitmap = PDFtoImage.Conversion.ToImage(
            stream,
            pageIndex,
            options: new PDFtoImage.RenderOptions(Dpi: (int)(72 * scale)));
        using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        return Task.FromResult(encoded.ToArray());
    }

    public void Dispose() { }
}

// =============================================================================
// Adapter B: Docnet.Core
// =============================================================================

class DocnetRenderer : IPdfRenderer
{
    public string Name => "Docnet.Core 2.6.0 (GowenGit/PDFium)";

    public int GetPageCount(string path)
    {
        using var library = Docnet.Core.DocLib.Instance;
        using var docReader = library.GetDocReader(path, new Docnet.Core.Models.PageDimensions(595, 842));
        return docReader.GetPageCount();
    }

    public Task<byte[]> RenderPageAsync(string path, int pageIndex, float scale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int width = (int)(595 * scale);
        int height = (int)(842 * scale);

        using var library = Docnet.Core.DocLib.Instance;
        using var docReader = library.GetDocReader(path, new Docnet.Core.Models.PageDimensions(width, height));
        using var pageReader = docReader.GetPageReader(pageIndex);

        int w = pageReader.GetPageWidth();
        int h = pageReader.GetPageHeight();
        byte[] rawBgra = pageReader.GetImage(); // BGRA

        using var bitmap = new SkiaSharp.SKBitmap(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        unsafe
        {
            fixed (byte* ptr = rawBgra)
            {
                var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                bitmap.InstallPixels(info, (nint)ptr, w * 4);
                using var snap = bitmap.PeekPixels();
                using var img = SkiaSharp.SKImage.FromPixels(snap);
                using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
                return Task.FromResult(data.ToArray());
            }
        }
    }

    public void Dispose() { }
}

// =============================================================================
// Benchmark harness
// =============================================================================

record BenchResult(
    string RendererName,
    string FixtureName,
    int PageCount,
    double P50Ms,
    double P95Ms,
    double PeakMemDeltaMb,
    bool NativeLoadSuccess,
    string? ErrorMessage = null
);

static class BenchmarkRunner
{
    private const float Scale = 1.0f;
    private const int WarmupIter = 2;
    private const int TimedIter = 10;
    private const int PagesPerFixture = 5;

    public static async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== S02 PDFium Wrapper Benchmark ===");
        Console.WriteLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"OS:   {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"Arch: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"CLR:  {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine();

        string fixturesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
        string[] fixtures = [
            Path.Combine(fixturesDir, "gc-simple-text.pdf"),
            Path.Combine(fixturesDir, "gc-large.pdf"),
            Path.Combine(fixturesDir, "gc-two-column.pdf"),
        ];

        foreach (string f in fixtures)
        {
            if (!File.Exists(f))
            {
                Console.Error.WriteLine($"FIXTURE NOT FOUND: {f}");
                Console.Error.WriteLine("Run S02.FixtureGen first.");
                Environment.Exit(1);
            }
        }

        var results = new List<BenchResult>();
        using var cts = new CancellationTokenSource();

        IPdfRenderer[] renderers = [new PdfToImageRenderer(), new DocnetRenderer()];

        foreach (var renderer in renderers)
        {
            Console.WriteLine($"\n--- {renderer.Name} ---");
            foreach (var fixturePath in fixtures)
            {
                var result = await RunBenchmarkAsync(renderer, fixturePath, cts.Token);
                results.Add(result);
            }
        }

        PrintSummary(results);
        CheckOsxArm64Rids();
    }

    private static async Task<BenchResult> RunBenchmarkAsync(IPdfRenderer renderer, string fixturePath, CancellationToken ct)
    {
        string fixtureName = Path.GetFileNameWithoutExtension(fixturePath);
        Console.Write($"  [{renderer.Name}] {fixtureName} ... ");

        int pageCount = 0;
        try
        {
            pageCount = renderer.GetPageCount(fixturePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LOAD FAILED: {ex.Message}");
            return new BenchResult(renderer.Name, fixtureName, 0, 0, 0, 0, false, ex.Message);
        }

        int pagesToRender = Math.Min(PagesPerFixture, pageCount);
        var allLatencies = new List<double>();

        // Warmup
        for (int iter = 0; iter < WarmupIter; iter++)
            for (int pg = 0; pg < pagesToRender; pg++)
                _ = await renderer.RenderPageAsync(fixturePath, pg, Scale, ct);

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long memBefore = GC.GetTotalMemory(forceFullCollection: false);
        long memPeak = memBefore;

        // Timed iterations
        for (int iter = 0; iter < TimedIter; iter++)
        {
            for (int pg = 0; pg < pagesToRender; pg++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _ = await renderer.RenderPageAsync(fixturePath, pg, Scale, ct);
                sw.Stop();
                allLatencies.Add(sw.Elapsed.TotalMilliseconds);
                long memNow = GC.GetTotalMemory(forceFullCollection: false);
                if (memNow > memPeak) memPeak = memNow;
            }
        }

        var (p50, p95) = ComputePercentiles(allLatencies);
        double peakDeltaMb = Math.Max(0, (memPeak - memBefore) / (1024.0 * 1024.0));

        Console.WriteLine($"pages={pageCount}, P50={p50:F1}ms, P95={p95:F1}ms, peakDelta={peakDeltaMb:F1}MB");
        return new BenchResult(renderer.Name, fixtureName, pageCount, p50, p95, peakDeltaMb, true);
    }

    private static (double p50, double p95) ComputePercentiles(List<double> samples)
    {
        var sorted = samples.OrderBy(x => x).ToList();
        int n = sorted.Count;
        return (sorted[(int)(n * 0.50)], sorted[Math.Min((int)(n * 0.95), n - 1)]);
    }

    private static void PrintSummary(List<BenchResult> results)
    {
        Console.WriteLine("\n=== RESULTS SUMMARY ===");
        Console.WriteLine($"{"Renderer",-42} {"Fixture",-22} {"Pages",5} {"P50(ms)",8} {"P95(ms)",8} {"PeakDelta(MB)",14} {"NativeLoad",11}");
        Console.WriteLine(new string('-', 112));
        foreach (var r in results)
        {
            Console.WriteLine($"{r.RendererName,-42} {r.FixtureName,-22} {r.PageCount,5} {r.P50Ms,8:F1} {r.P95Ms,8:F1} {r.PeakMemDeltaMb,14:F1} {r.NativeLoadSuccess,11}");
        }
        Console.WriteLine();
    }

    private static void CheckOsxArm64Rids()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string nugetCache = Path.Combine(userHome, ".nuget", "packages");

        bool pdfToImageOsxArm64 = false;
        string bblanchonMacDir = Path.Combine(nugetCache, "bblanchon.pdfium.macos");
        if (Directory.Exists(bblanchonMacDir))
        {
            foreach (var vDir in Directory.EnumerateDirectories(bblanchonMacDir))
            {
                string runtimesPath = Path.Combine(vDir, "runtimes");
                if (Directory.Exists(runtimesPath) && Directory.Exists(Path.Combine(runtimesPath, "osx-arm64")))
                {
                    pdfToImageOsxArm64 = true;
                    break;
                }
            }
        }

        bool docnetOsxArm64 = false;
        string docnetDir = Path.Combine(nugetCache, "docnet.core");
        if (Directory.Exists(docnetDir))
        {
            foreach (var vDir in Directory.EnumerateDirectories(docnetDir))
            {
                string runtimesPath = Path.Combine(vDir, "runtimes");
                if (Directory.Exists(runtimesPath) && Directory.Exists(Path.Combine(runtimesPath, "osx-arm64")))
                {
                    docnetOsxArm64 = true;
                    break;
                }
            }
        }

        Console.WriteLine($"osx-arm64 RID present -- PDFtoImage (via bblanchon.pdfium.macos): {(pdfToImageOsxArm64 ? "YES" : "NO")}");
        Console.WriteLine($"osx-arm64 RID present -- Docnet.Core (built-in):                  {(docnetOsxArm64 ? "YES" : "NO")}");
        Console.WriteLine("\nBenchmark complete.");
    }
}
