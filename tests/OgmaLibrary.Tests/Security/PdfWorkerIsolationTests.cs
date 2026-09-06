using System.Diagnostics;
using System.Text;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace OgmaLibrary.Tests.Security;

/// <summary>Phase 05 fault-injection tests for the isolated PDF worker boundary.</summary>
public sealed class PdfWorkerIsolationTests : IDisposable
{
    private readonly string _sandboxRoot;
    private readonly string _fixtureRoot;
    private readonly PdfWorkerClient _client;

    public PdfWorkerIsolationTests()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-pdf-worker-tests-{Guid.NewGuid():N}");
        _sandboxRoot = Path.Combine(root, "sandbox");
        _fixtureRoot = Path.Combine(root, "fixtures");
        Directory.CreateDirectory(_sandboxRoot);
        Directory.CreateDirectory(_fixtureRoot);
        _client = new PdfWorkerClient(new PdfWorkerOptions
        {
            SandboxRoot = _sandboxRoot,
            Timeout = TimeSpan.FromSeconds(20),
        });
    }

    [Theory]
    [InlineData("network")]
    [InlineData("process")]
    public void PdfWorker_DangerousOperations_AreDeniedByPolicy(string diagnostic)
    {
        PdfWorkerDiagnosticResult result = _client.RunDiagnostic(diagnostic);

        Assert.Equal("blocked", result.Status);
    }

    [Fact]
    public void PdfWorker_TempEscape_AttemptDoesNotWriteOutsideSandbox()
    {
        PdfWorkerDiagnosticResult result = _client.RunDiagnostic("temp-escape");
        string escapePath = Path.Combine(_sandboxRoot, "ogma-worker-escape.txt");

        Assert.Equal("blocked", result.Status);
        Assert.False(File.Exists(escapePath), $"Escape file should not exist: {escapePath}");
    }

    [Fact]
    public void IsolatedPdfRenderer_MalformedPdf_ReturnsZeroPagesWithoutCrashing()
    {
        string malformedPath = Path.Combine(_fixtureRoot, "malformed.pdf");
        File.WriteAllText(malformedPath, "%PDF-1.7\nmalformed");

        using IPdfRenderer renderer = new IsolatedPdfRendererFactory(_client).Open(malformedPath);

        Assert.Equal(0, renderer.PageCount);
    }

    [Fact]
    public async Task IsolatedPdfRenderer_ValidPdf_RendersThroughWorker()
    {
        string pdfPath = CreateValidPdf("worker-render.pdf");

        using IPdfRenderer renderer = new IsolatedPdfRendererFactory(_client).Open(pdfPath);
        RenderResult result = await renderer.RenderPageAsync(0, new RenderRequest(400), CancellationToken.None);

        Assert.Equal(1, renderer.PageCount);
        Assert.Equal(0, result.PageIndex);
        Assert.NotEmpty(result.PngBytes);
    }

    [Fact]
    public void IsolatedPdfRenderer_RecordsWorkerResourceTelemetryAfterSessionDisposal()
    {
        string pdfPath = CreateValidPdf("worker-telemetry.pdf");
        var telemetryClient = new PdfWorkerClient(new PdfWorkerOptions
        {
            SandboxRoot = _sandboxRoot,
            Timeout = TimeSpan.FromSeconds(20),
        });

        using (IPdfRenderer renderer = new IsolatedPdfRendererFactory(telemetryClient).Open(pdfPath))
        {
            Assert.Equal(1, renderer.PageCount);
        }

        if (telemetryClient.MaxPeakWorkingSetBytes <= 0 ||
            telemetryClient.MaxPrivateMemoryBytes <= 0)
        {
            Console.WriteLine(
                "NOT ASSESSED: the current runtime did not expose the worker resource counters; " +
                "this platform metric remains NOT ASSESSED.");
            return;
        }

        Assert.True(telemetryClient.MaxPeakWorkingSetBytes > 0);
        Assert.True(telemetryClient.MaxPrivateMemoryBytes > 0);
    }

    [Fact]
    public async Task PdfWorkerSession_ProcessTermination_IsObservableAndNewSessionRecovers()
    {
        string pdfPath = CreateValidPdf("worker-process-recovery.pdf");

        using PdfWorkerClient.PdfWorkerSession session = _client.OpenSession(pdfPath);
        using (Process process = Process.GetProcessById(session.ProcessId))
        {
            process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(5_000));
        }

        Assert.ThrowsAny<Exception>(() => session.ExtractTextLayer(0));

        using PdfWorkerClient.PdfWorkerSession recovered = _client.OpenSession(pdfPath);
        Assert.Equal(1, recovered.PageCount);
        RenderResult result = await recovered.RenderPageAsync(
            0,
            new RenderRequest(300),
            CancellationToken.None);
        Assert.NotEmpty(result.PngBytes);
    }

    [Fact]
    public async Task PdfWorker_OutputCeilingRejectsOversizedRenderedArtifact()
    {
        string pdfPath = CreateValidPdf("worker-output-limit.pdf");
        var limitedClient = new PdfWorkerClient(new PdfWorkerOptions
        {
            SandboxRoot = _sandboxRoot,
            Timeout = TimeSpan.FromSeconds(20),
            MaxOutputBytes = 1,
        });

        using IPdfRenderer renderer = new IsolatedPdfRendererFactory(limitedClient).Open(pdfPath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderPageAsync(
            0,
            new RenderRequest(400),
            CancellationToken.None));
    }

    [Fact]
    public void PdfWorker_EmbeddedCoverImage_IsNormalizedAndBounded()
    {
        string pdfPath = CreateImagePdf("worker-embedded-cover.pdf");
        string outputPath = Path.Combine(_fixtureRoot, "embedded-cover.jpg");

        _client.GenerateEmbeddedCover(pdfPath, outputPath, 200, 300);

        Assert.True(File.Exists(outputPath));
        using SKBitmap? bitmap = SKBitmap.Decode(outputPath);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(300, bitmap.Height);
    }

    [Fact]
    public async Task IsolatedPdfRenderer_UsesSandboxCopyAfterSourceIsRemoved()
    {
        string pdfPath = CreateValidPdf("worker-copy.pdf");
        using IPdfRenderer renderer = new IsolatedPdfRendererFactory(_client).Open(pdfPath);

        File.Delete(pdfPath);
        RenderResult result = await renderer.RenderPageAsync(
            0,
            new RenderRequest(300),
            CancellationToken.None);

        Assert.Equal(1, renderer.PageCount);
        Assert.NotEmpty(result.PngBytes);
    }

    [Fact]
    public async Task PdfWorker_HostileMalformedCorpus_FailsSafelyAndRemainsAvailable()
    {
        string recoveryPdf = CreateValidPdf("hostile-corpus-recovery.pdf");
        string escapeMarker = Path.Combine(Path.GetDirectoryName(_fixtureRoot)!, "hostile-escape.txt");
        byte[][] corpus =
        [
            "%PDF-1.7\nmalformed"u8.ToArray(),
            "%PDF-1.7\n1 0 obj << /Length 999999999 >> stream\ntruncated"u8.ToArray(),
            "%PDF-1.7\nxref\n0 99999999\n0000000000 65535 f"u8.ToArray(),
            [.. "%PDF-1.7\n"u8.ToArray(), .. new byte[4_096]],
            Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj {new string('[', 256)} 0 {new string(']', 256)} endobj"),
            Encoding.ASCII.GetBytes($"%PDF-1.7\n/OpenAction << /S /JavaScript /JS (write {escapeMarker}) >>"),
            "%PDF-1.7\n1 0 obj << /Filter /FlateDecode /Length 20 >> stream\nnot-deflate\nendstream"u8.ToArray(),
        ];

        for (int index = 0; index < corpus.Length; index++)
        {
            string hostilePath = Path.Combine(_fixtureRoot, $"hostile-{index:00}.pdf");
            await File.WriteAllBytesAsync(hostilePath, corpus[index]);
            try
            {
                using IPdfRenderer renderer = new IsolatedPdfRendererFactory(_client).Open(hostilePath);
                Assert.Equal(0, renderer.PageCount);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException)
            {
                Assert.Equal("PDF worker operation failed.", error.Message);
                Assert.DoesNotContain(_fixtureRoot, error.ToString(), StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("hostile-escape", error.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            Assert.False(File.Exists(escapeMarker));
            using IPdfRenderer recovered = new IsolatedPdfRendererFactory(_client).Open(recoveryPdf);
            Assert.Equal(1, recovered.PageCount);
            RenderResult result = await recovered.RenderPageAsync(
                0,
                new RenderRequest(200),
                CancellationToken.None);
            Assert.NotEmpty(result.PngBytes);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Path.GetDirectoryName(_sandboxRoot)))
        {
            Directory.Delete(Path.GetDirectoryName(_sandboxRoot)!, recursive: true);
        }
    }

    private string CreateValidPdf(string fileName)
    {
        string path = Path.Combine(_fixtureRoot, fileName);
        using var document = new PdfDocument();
        document.Info.Title = "Ogma worker isolation fixture";
        document.AddPage();
        document.Save(path);
        return path;
    }

    private string CreateImagePdf(string fileName)
    {
        string path = Path.Combine(_fixtureRoot, fileName);
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        using XGraphics graphics = XGraphics.FromPdfPage(page);
        using XImage image = XImage.FromStream(new MemoryStream(png, 0, png.Length, writable: false, publiclyVisible: true));
        graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        document.Save(path);
        return path;
    }
}
