using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Pdf;

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
}
