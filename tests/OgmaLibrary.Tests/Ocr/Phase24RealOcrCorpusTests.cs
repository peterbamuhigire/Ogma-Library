using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;
using Xunit;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Packaged-Tesseract acceptance for a deterministic scanned fixture.</summary>
public sealed class Phase24RealOcrCorpusTests
{
    [Fact]
    public async Task PackagedTesseract_RecognizesExpectedWordsFromGeneratedScannedFixture()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine(
                "NOT ASSESSED: the Tesseract 5.2.0 package supplies native binaries only for Windows; " +
                "macOS/Linux OCR requires a supported native runtime package.");
            return;
        }

        string corpusRoot = FindCorpusRoot();
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        string sandboxRoot = Path.Combine(Path.GetTempPath(), $"ogma-phase24-real-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandboxRoot);
        string pdfPath = Path.Combine(sandboxRoot, "scanned-text-fixture.pdf");

        try
        {
            string[] expectedWords = File.ReadAllLines(Path.Combine(corpusRoot, "expected-words.txt"))
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word.Trim().ToLowerInvariant())
                .ToArray();
            CreateScannedPdf(pdfPath, expectedWords);

            var worker = new PdfWorkerClient(new PdfWorkerOptions
            {
                SandboxRoot = sandboxRoot,
                Timeout = TimeSpan.FromSeconds(30),
                CpuTimeLimit = TimeSpan.FromSeconds(15),
            });
            var rendererFactory = new IsolatedPdfRendererFactory(worker);
            var provider = new TesseractOcrProvider(tessdataPath);
            var recognized = new List<OcrPageResult>();

            using (IPdfRenderer renderer = rendererFactory.Open(pdfPath))
            {
                for (int page = 0; page < renderer.PageCount; page++)
                {
                    RenderResult rendered = await renderer.RenderPageAsync(
                        page,
                        new RenderRequest(1_200),
                        CancellationToken.None);
                    await using var image = new MemoryStream(rendered.PngBytes, writable: false);
                    recognized.Add(await provider.RecognizeAsync(image, "eng"));
                }
            }

            string text = string.Join(
                ' ',
                recognized.Select(result => result.Text)).ToLowerInvariant();

            Assert.NotEmpty(recognized);
            Assert.All(recognized, result => Assert.True(
                result.Confidence >= 0.75,
                $"OCR confidence {result.Confidence:F3} was below the 0.75 selection threshold."));
            Assert.All(expectedWords, word => Assert.Contains(word, text, StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(sandboxRoot))
            {
                Directory.Delete(sandboxRoot, recursive: true);
            }
        }
    }

    private static string FindCorpusRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "tests", "golden-corpus", "ocr-pipeline");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find tests/golden-corpus/ocr-pipeline.");
    }

    private static void CreateScannedPdf(string pdfPath, IReadOnlyList<string> words)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1_200, 1_700, SKColorType.Rgba8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };
        using var font = new SKFont(SKTypeface.Default, 80);
        float baseline = 220;
        foreach (string word in words)
        {
            surface.Canvas.DrawText(word, 120, baseline, SKTextAlign.Left, font, paint);
            baseline += 260;
        }

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] png = encoded.ToArray();
        using var imageStream = new MemoryStream(png, 0, png.Length, writable: false, publiclyVisible: true);
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        using XGraphics graphics = XGraphics.FromPdfPage(page);
        using XImage pdfImage = XImage.FromStream(imageStream);
        graphics.DrawImage(pdfImage, 0, 0, page.Width.Point, page.Height.Point);
        document.Save(pdfPath);
    }
}
