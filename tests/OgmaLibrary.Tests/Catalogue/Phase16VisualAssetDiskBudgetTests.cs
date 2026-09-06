using System.Diagnostics;
using System.Security.Cryptography;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Infrastructure.Sidecar;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Real-worker disk-budget evidence for the bounded Phase 16 variants.</summary>
public sealed class Phase16VisualAssetDiskBudgetTests
{
    private const long PerBookDiskBudgetBytes = 512 * 1024;
    private const long TargetLibrarySize = 50_000;

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task GeneratedVariants_StayWithinDiskBudgetAcrossPdfCorpus()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-phase16-disk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            context.Database.EnsureCreated();
            string[] corpus = FindPdfCorpus();
            var sidecar = new SidecarService(root);
            var worker = new PdfWorkerClient(new PdfWorkerOptions
            {
                SandboxRoot = Path.Combine(root, "worker-sandbox"),
                Timeout = TimeSpan.FromSeconds(30),
            });
            var assets = new VisualAssetService(context, root);
            var thumbnails = new ThumbnailService(sidecar, worker, assets);
            var spines = new SpineService(sidecar, worker, assets);
            var measured = new List<(string FileName, long Bytes, long Milliseconds)>();

            for (int index = 0; index < corpus.Length; index++)
            {
                string pdfPath = corpus[index];
                string hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(pdfPath)));
                string bookId = $"PH16-DISK-{index:000}";
                context.Books.Add(new BookRow { BookId = bookId, Title = Path.GetFileName(pdfPath), Status = 0 });
                await context.SaveChangesAsync();
                Stopwatch stopwatch = Stopwatch.StartNew();

                AssertSuccess(await thumbnails.GenerateCoverVariantAsync(
                    bookId, hash, pdfPath, VisualAssetVariants.CoverDefault.Name));
                AssertSuccess(await thumbnails.GenerateCoverVariantAsync(
                    bookId, hash, pdfPath, VisualAssetVariants.CoverDetail.Name));
                AssertSuccess(await spines.GenerateSpineVariantAsync(
                    bookId, hash, pdfPath, VisualAssetVariants.SpineDefault.Name));
                AssertSuccess(await spines.GenerateSpineVariantAsync(
                    bookId, hash, pdfPath, VisualAssetVariants.SpineRetina.Name));

                stopwatch.Stop();
                VisualAssetDescriptor[] variants =
                [
                    (await assets.GetVariantAsync(bookId, VisualAssetKind.Cover, "default"))!,
                    (await assets.GetVariantAsync(bookId, VisualAssetKind.Cover, "detail"))!,
                    (await assets.GetVariantAsync(bookId, VisualAssetKind.Spine, "default"))!,
                    (await assets.GetVariantAsync(bookId, VisualAssetKind.Spine, "retina"))!,
                ];
                Assert.DoesNotContain(variants, descriptor => descriptor is null);
                long bytes = variants.Sum(descriptor => new FileInfo(
                    Path.Combine(root, descriptor.RelativePath.Replace('/', Path.DirectorySeparatorChar))).Length);
                measured.Add((Path.GetFileName(pdfPath), bytes, stopwatch.ElapsedMilliseconds));
                Assert.True(
                    bytes <= PerBookDiskBudgetBytes,
                    $"{Path.GetFileName(pdfPath)} generated {bytes} bytes, above the " +
                    $"{PerBookDiskBudgetBytes}-byte four-variant budget.");
            }

            long maximumBytes = measured.Max(result => result.Bytes);
            double projectedGiB = maximumBytes * TargetLibrarySize / (1024d * 1024d * 1024d);
            Console.WriteLine(
                $"Phase16 real-worker disk budget: corpus={measured.Count}, " +
                $"maxBytesPerBook={maximumBytes}, projected50kGiB={projectedGiB:F3}, " +
                $"samples={string.Join(';', measured.Select(result => $"{result.FileName}:{result.Bytes}B/{result.Milliseconds}ms"))}");
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AssertSuccess((bool Success, string? ErrorMessage) result) =>
        Assert.True(result.Success, result.ErrorMessage);

    private static string[] FindPdfCorpus()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "spikes", "s02-pdfium", "fixtures");
            if (Directory.Exists(candidate))
            {
                string[] corpus = Directory.GetFiles(candidate, "gc-*.pdf", SearchOption.TopDirectoryOnly);
                Assert.Equal(3, corpus.Length);
                return corpus.Order(StringComparer.Ordinal).ToArray();
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find spikes/s02-pdfium/fixtures.");
    }
}
